#include "AccountFile.h"
#include "utils/Strings.h"

#include <algorithm>
#include <cstddef>
#include <string>

namespace gwl {

namespace {

const std::string kMarker = "encrypted";
const std::size_t kSaltLength = 16;
const std::size_t kIvLength = 16;
const std::uint32_t kIterations = 600000;
const std::uint8_t kLegacyIv[16] = {0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85, 0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85};
const std::string kLegacyPrefix = "SHIT";

Bytes slice(const Bytes& data, std::size_t from, std::size_t to) {
    return Bytes(data.begin() + static_cast<std::ptrdiff_t>(from), data.begin() + static_cast<std::ptrdiff_t>(to));
}

// The PBKDF2 password: the hash's bytes read as UTF-8 text, as the C# AccountFile.DeriveKey does.
Bytes keyPassword(const PasswordHash& password) {
    bool invalid = false;
    const Bytes hash(password.begin(), password.end());
    return bytesOf(decodeUtf8Lossy(hash, invalid));
}

Bytes deriveKey(const PasswordHash& password, const Bytes& salt) {
    return pbkdf2Sha256(keyPassword(password), salt, kIterations, 32);
}

// Opens the salt, IV and ciphertext form. Nothing when the data is too short, the key is wrong, or the text isn't UTF-8.
std::optional<std::string> decryptSalted(const Bytes& data, const PasswordHash& password) {
    if (data.size() < kSaltLength + kIvLength) {
        return std::nullopt;
    }
    const Bytes salt = slice(data, 0, kSaltLength);
    const Bytes iv = slice(data, kSaltLength, kSaltLength + kIvLength);
    const Bytes cipher = slice(data, kSaltLength + kIvLength, data.size());
    auto plain = aes256CbcDecrypt(deriveKey(password, salt), iv, cipher);
    if (!plain) {
        return std::nullopt;
    }
    return decodeUtf8Strict(*plain);
}

// The legacy form: the raw hash is the key, the IV is fixed, and the JSON follows a "SHIT" prefix.
std::optional<std::string> decryptLegacy(const Bytes& data, const PasswordHash& password) {
    const Bytes key(password.begin(), password.end());
    auto plain = aes256CbcDecrypt(key, Bytes(kLegacyIv, kLegacyIv + 16), data);
    if (!plain) {
        return std::nullopt;
    }
    auto text = decodeUtf8Strict(*plain);
    if (!text || text->compare(0, kLegacyPrefix.size(), kLegacyPrefix) != 0) {
        return std::nullopt;
    }
    return text->substr(kLegacyPrefix.size());
}

}  // namespace

PasswordHash hashPassword(const std::string& password) {
    const Bytes hash = sha256(bytesOf(password));
    PasswordHash result{};
    std::copy(hash.begin(), hash.end(), result.begin());
    return result;
}

bool hasEncryptionMarker(const Bytes& raw) {
    return raw.size() >= kMarker.size() && std::equal(kMarker.begin(), kMarker.end(), raw.begin());
}

Bytes encryptAccountsFile(const std::string& json, const PasswordHash& password) {
    const Bytes salt = randomBytes(kSaltLength);
    const Bytes iv = randomBytes(kIvLength);
    const Bytes cipher = aes256CbcEncrypt(deriveKey(password, salt), iv, bytesOf(json));

    Bytes output = bytesOf(kMarker);
    output.insert(output.end(), salt.begin(), salt.end());
    output.insert(output.end(), iv.begin(), iv.end());
    output.insert(output.end(), cipher.begin(), cipher.end());
    return output;
}

std::optional<std::string> decryptAccountsFile(const Bytes& raw, const PasswordHash& password) {
    if (hasEncryptionMarker(raw)) {
        return decryptSalted(slice(raw, kMarker.size(), raw.size()), password);
    }
    if (auto text = decryptSalted(raw, password)) {
        return text;
    }
    return decryptLegacy(raw, password);
}

}  // namespace gwl
