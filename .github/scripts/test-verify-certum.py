"""Exercise Linux verification against a real signed PE without signing again."""

import hashlib
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile

from asn1crypto import cms


signed = Path(sys.argv[1])
verify = Path(__file__).with_name("verify-certum.sh")
original = signed.read_bytes()
pe = struct.unpack_from("<I", original, 0x3C)[0]
optional = pe + 24
magic = struct.unpack_from("<H", original, optional)[0]
assert magic in (0x10B, 0x20B), "Unsupported PE optional header"
directories = optional + (112 if magic == 0x20B else 96)
security = directories + 8 * 4
cert_offset, cert_size = struct.unpack_from("<II", original, security)
assert cert_offset and cert_offset + cert_size == len(original), "Expected trailing certificate table"
length, revision, kind = struct.unpack_from("<IHH", original, cert_offset)
assert revision == 0x200 and kind == 2
signature = cms.ContentInfo.load(original[cert_offset + 8:cert_offset + length])
signer = signature["content"]["signer_infos"][0]
sid = signer["sid"].chosen
leaf = next(
    entry.chosen for entry in signature["content"]["certificates"]
    if entry.name == "certificate"
    and entry.chosen.serial_number == sid["serial_number"].native
    and entry.chosen.issuer == sid["issuer"]
)
thumb = hashlib.sha1(leaf.dump()).hexdigest()
assert thumb.upper() == "".join(os.environ["CERTUM_CERT_SHA1"].split()).upper()


def check(file, expected, label, env=None):
    result = subprocess.run(
        ["bash", str(verify), str(file)], capture_output=True, text=True,
        env=os.environ | (env or {}),
    )
    assert (result.returncode == 0) == expected, result.stdout + result.stderr
    print(f"PASS: {label}")
    return result.stdout + result.stderr


def with_signature(signature):
    der = signature.dump()
    certificate = struct.pack("<IHH", len(der) + 8, revision, kind) + der
    certificate += b"\0" * (-len(certificate) % 8)
    result = bytearray(original[:cert_offset]) + certificate
    struct.pack_into("<II", result, security, cert_offset, len(certificate))
    return result


check(signed, True, "real Certum signature, expected signer and verified timestamp")
with tempfile.TemporaryDirectory() as directory:
    directory = Path(directory)
    tampered = bytearray(original)
    sections = optional + struct.unpack_from("<H", original, pe + 20)[0]
    raw_offset = struct.unpack_from("<I", original, sections + 20)[0]
    tampered[raw_offset] ^= 1
    tampered_file = directory / "tampered.exe"
    tampered_file.write_bytes(tampered)
    output = check(tampered_file, False, "tampered PE section rejected")
    assert "MISMATCH" in output

    output = check(signed, False, "wrong signer rejected", {"CERTUM_CERT_SHA1": "0" * 40})
    assert "Leaf hash match: failed" in output

    corrupt_signature = cms.ContentInfo.load(signature.dump())
    attrs = corrupt_signature["content"]["signer_infos"][0]["unsigned_attrs"]
    timestamp = next(attr["values"][0] for attr in attrs if attr["type"].native == "microsoft_time_stamp_token")
    tsa_signer = timestamp["content"]["signer_infos"][0]
    tsa_bytes = bytearray(tsa_signer["signature"].native)
    tsa_bytes[0] ^= 1
    tsa_signer["signature"] = bytes(tsa_bytes)
    corrupt_file = directory / "corrupt-timestamp.exe"
    corrupt_file.write_bytes(with_signature(corrupt_signature))
    output = check(corrupt_file, False, "cryptographically invalid timestamp rejected")
    assert "Timestamp Server Signature verification: failed" in output

    # Only remove the unauthenticated timestamp; retain the valid cloud signature.
    signer["unsigned_attrs"] = None
    no_timestamp_file = directory / "no-timestamp.exe"
    no_timestamp_file.write_bytes(with_signature(signature))
    output = check(no_timestamp_file, False, "valid but untimestamped signature rejected")
    assert "Signature verification: ok" in output
    assert "Missing verified timestamp" in output

    unrelated_ca = "/etc/ssl/certs/ISRG_Root_X1.pem"
    assert Path(unrelated_ca).is_file()
    output = check(signed, False, "untrusted certificate chain rejected", {"SSL_CERT_FILE": unrelated_ca})
    assert "unable to get local issuer certificate" in output
