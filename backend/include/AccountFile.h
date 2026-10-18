// The encrypted Accounts.json format, byte-compatible with the C# and Rust launchers. An encrypted file starts with the
// "encrypted" marker, then a 16-byte salt, a 16-byte IV and AES-256-CBC ciphertext. The key is PBKDF2-HMAC-SHA256 over the
// password's hash, read as text. Older files have no marker, and an older one still uses a fixed IV and a "SHIT" prefix.

#pragma once

#include <array>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include "Crypto.h"

namespace gwl {

// The SHA-256 of the master password. It's the only form of the password the launcher keeps.
using PasswordHash = std::array<std::uint8_t, 32>;

PasswordHash hashPassword(const std::string& password);

// Whether the file starts with the marker of the current format.
bool hasEncryptionMarker(const Bytes& raw);

// The file bytes of a JSON document, encrypted with the password's hash in the current format.
Bytes encryptAccountsFile(const std::string& json, const PasswordHash& password);

// The JSON in an encrypted file, trying the current format and then the older ones. Nothing when the password doesn't open it.
std::optional<std::string> decryptAccountsFile(const Bytes& raw, const PasswordHash& password);

}  // namespace gwl
