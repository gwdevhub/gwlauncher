#include "Crypto.h"

#include <windows.h>
#include <bcrypt.h>

#include <stdexcept>
#include <string>

namespace gwl {

namespace {

[[noreturn]] void fail(const char* what) {
    throw std::runtime_error(std::string(what) + " isn't available on this computer.");
}

// Opens a BCrypt algorithm and closes it when it goes out of scope.
class Algorithm {
public:
    Algorithm(LPCWSTR name, ULONG flags) {
        if (!BCRYPT_SUCCESS(BCryptOpenAlgorithmProvider(&handle_, name, nullptr, flags))) {
            fail("The system's algorithm");
        }
    }
    ~Algorithm() { BCryptCloseAlgorithmProvider(handle_, 0); }
    Algorithm(const Algorithm&) = delete;
    Algorithm& operator=(const Algorithm&) = delete;
    BCRYPT_ALG_HANDLE get() const { return handle_; }

private:
    BCRYPT_ALG_HANDLE handle_ = nullptr;
};

// A symmetric key made from its bytes, destroyed when it goes out of scope.
class SymmetricKey {
public:
    SymmetricKey(BCRYPT_ALG_HANDLE algorithm, const Bytes& key) {
        if (!BCRYPT_SUCCESS(BCryptGenerateSymmetricKey(algorithm, &handle_, nullptr, 0, const_cast<PUCHAR>(key.data()),
                                                       static_cast<ULONG>(key.size()), 0))) {
            fail("The system's key");
        }
    }
    ~SymmetricKey() { BCryptDestroyKey(handle_); }
    SymmetricKey(const SymmetricKey&) = delete;
    SymmetricKey& operator=(const SymmetricKey&) = delete;
    BCRYPT_KEY_HANDLE get() const { return handle_; }

private:
    BCRYPT_KEY_HANDLE handle_ = nullptr;
};

// Runs AES-256-CBC with PKCS#7 padding in the given direction. Returns nothing when BCrypt reports an error.
std::optional<Bytes> runCipher(const Bytes& key, const Bytes& iv, const Bytes& input, bool encrypt) {
    Algorithm aes(BCRYPT_AES_ALGORITHM, 0);
    if (!BCRYPT_SUCCESS(BCryptSetProperty(aes.get(), BCRYPT_CHAINING_MODE, reinterpret_cast<PUCHAR>(const_cast<wchar_t*>(BCRYPT_CHAIN_MODE_CBC)),
                                          sizeof(BCRYPT_CHAIN_MODE_CBC), 0))) {
        fail("The system's cipher mode");
    }
    SymmetricKey symmetric(aes.get(), key);

    // BCrypt updates the IV it's given, so each call gets its own copy.
    Bytes ivCopy = iv;
    Bytes output(input.size() + 32);
    ULONG written = 0;
    const NTSTATUS status = encrypt ? BCryptEncrypt(symmetric.get(), const_cast<PUCHAR>(input.data()), static_cast<ULONG>(input.size()), nullptr,
                                                    ivCopy.data(), static_cast<ULONG>(ivCopy.size()), output.data(),
                                                    static_cast<ULONG>(output.size()), &written, BCRYPT_BLOCK_PADDING)
                                    : BCryptDecrypt(symmetric.get(), const_cast<PUCHAR>(input.data()), static_cast<ULONG>(input.size()), nullptr,
                                                    ivCopy.data(), static_cast<ULONG>(ivCopy.size()), output.data(),
                                                    static_cast<ULONG>(output.size()), &written, BCRYPT_BLOCK_PADDING);
    if (!BCRYPT_SUCCESS(status)) {
        return std::nullopt;
    }
    output.resize(written);
    return output;
}

}  // namespace

Bytes randomBytes(std::size_t count) {
    Bytes bytes(count);
    if (count > 0 && !BCRYPT_SUCCESS(BCryptGenRandom(nullptr, bytes.data(), static_cast<ULONG>(count), BCRYPT_USE_SYSTEM_PREFERRED_RNG))) {
        fail("The system's random numbers");
    }
    return bytes;
}

Bytes sha256(const Bytes& data) {
    Algorithm sha(BCRYPT_SHA256_ALGORITHM, 0);
    Bytes hash(32);
    if (!BCRYPT_SUCCESS(BCryptHash(sha.get(), nullptr, 0, const_cast<PUCHAR>(data.data()), static_cast<ULONG>(data.size()), hash.data(),
                                   static_cast<ULONG>(hash.size())))) {
        fail("The system's SHA-256");
    }
    return hash;
}

Bytes pbkdf2Sha256(const Bytes& password, const Bytes& salt, std::uint32_t iterations, std::size_t length) {
    Algorithm hmac(BCRYPT_SHA256_ALGORITHM, BCRYPT_ALG_HANDLE_HMAC_FLAG);
    Bytes key(length);
    if (!BCRYPT_SUCCESS(BCryptDeriveKeyPBKDF2(hmac.get(), const_cast<PUCHAR>(password.data()), static_cast<ULONG>(password.size()),
                                              const_cast<PUCHAR>(salt.data()), static_cast<ULONG>(salt.size()), iterations, key.data(),
                                              static_cast<ULONG>(key.size()), 0))) {
        fail("The system's key derivation");
    }
    return key;
}

Bytes aes256CbcEncrypt(const Bytes& key, const Bytes& iv, const Bytes& plain) {
    auto cipher = runCipher(key, iv, plain, true);
    if (!cipher) {
        fail("The system's cipher");
    }
    return *cipher;
}

std::optional<Bytes> aes256CbcDecrypt(const Bytes& key, const Bytes& iv, const Bytes& cipher) {
    return runCipher(key, iv, cipher, false);
}

}  // namespace gwl
