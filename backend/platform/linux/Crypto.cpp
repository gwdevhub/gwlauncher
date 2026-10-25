#include "Crypto.h"

#include <openssl/evp.h>
#include <openssl/rand.h>

#include <stdexcept>

namespace gwl {

namespace {

// Frees a cipher context when it goes out of scope.
class CipherContext {
public:
    CipherContext() : ctx_(EVP_CIPHER_CTX_new()) {
        if (!ctx_) {
            throw std::runtime_error("The cipher couldn't be started.");
        }
    }
    ~CipherContext() { EVP_CIPHER_CTX_free(ctx_); }
    CipherContext(const CipherContext&) = delete;
    CipherContext& operator=(const CipherContext&) = delete;
    EVP_CIPHER_CTX* get() const { return ctx_; }

private:
    EVP_CIPHER_CTX* ctx_;
};

// Runs AES-256-CBC in the given direction. Returns nothing when the cipher reports an error, such as bad padding.
std::optional<Bytes> runCipher(const Bytes& key, const Bytes& iv, const Bytes& input, bool encrypt) {
    CipherContext context;
    if (EVP_CipherInit_ex(context.get(), EVP_aes_256_cbc(), nullptr, key.data(), iv.data(), encrypt ? 1 : 0) != 1) {
        return std::nullopt;
    }
    Bytes output(input.size() + 32);
    int written = 0;
    int total = 0;
    if (EVP_CipherUpdate(context.get(), output.data(), &written, input.data(), static_cast<int>(input.size())) != 1) {
        return std::nullopt;
    }
    total = written;
    if (EVP_CipherFinal_ex(context.get(), output.data() + total, &written) != 1) {
        return std::nullopt;
    }
    total += written;
    output.resize(static_cast<std::size_t>(total));
    return output;
}

}  // namespace

Bytes randomBytes(std::size_t count) {
    Bytes bytes(count);
    if (count > 0 && RAND_bytes(bytes.data(), static_cast<int>(count)) != 1) {
        throw std::runtime_error("The system's random numbers couldn't be read.");
    }
    return bytes;
}

Bytes sha256(const Bytes& data) {
    Bytes hash(32);
    unsigned int length = 0;
    if (EVP_Digest(data.data(), data.size(), hash.data(), &length, EVP_sha256(), nullptr) != 1) {
        throw std::runtime_error("SHA-256 failed.");
    }
    return hash;
}

Bytes pbkdf2Sha256(const Bytes& password, const Bytes& salt, std::uint32_t iterations, std::size_t length) {
    Bytes key(length);
    const int ok = PKCS5_PBKDF2_HMAC(reinterpret_cast<const char*>(password.data()), static_cast<int>(password.size()), salt.data(),
                                    static_cast<int>(salt.size()), static_cast<int>(iterations), EVP_sha256(),
                                    static_cast<int>(length), key.data());
    if (ok != 1) {
        throw std::runtime_error("Key derivation failed.");
    }
    return key;
}

Bytes aes256CbcEncrypt(const Bytes& key, const Bytes& iv, const Bytes& plain) {
    auto cipher = runCipher(key, iv, plain, true);
    if (!cipher) {
        throw std::runtime_error("The cipher couldn't encrypt.");
    }
    return *cipher;
}

std::optional<Bytes> aes256CbcDecrypt(const Bytes& key, const Bytes& iv, const Bytes& cipher) {
    return runCipher(key, iv, cipher, false);
}

}  // namespace gwl
