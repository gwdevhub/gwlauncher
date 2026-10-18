// The primitives the encrypted accounts file and the remembered password use. Each platform implements them in
// platform/<os>/Crypto.cpp: BCrypt on Windows, and the system's libcrypto on Linux, which is loaded when first used.

#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>
#include <vector>

#include "utils/Strings.h"

namespace gwl {

// Throws std::runtime_error when the system can't supply the bytes.
Bytes randomBytes(std::size_t count);

// Throws std::runtime_error when the system's hash isn't available.
Bytes sha256(const Bytes& data);

// Throws std::runtime_error when the system's key derivation isn't available.
Bytes pbkdf2Sha256(const Bytes& password, const Bytes& salt, std::uint32_t iterations, std::size_t length);

// AES-256 in CBC mode with PKCS#7 padding. Encrypting throws std::runtime_error when the system's cipher isn't available.
Bytes aes256CbcEncrypt(const Bytes& key, const Bytes& iv, const Bytes& plain);

// Nothing when the padding or the length is wrong, which is what a wrong key looks like.
std::optional<Bytes> aes256CbcDecrypt(const Bytes& key, const Bytes& iv, const Bytes& cipher);

}  // namespace gwl
