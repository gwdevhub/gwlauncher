#include "Crypto.h"

#include <dlfcn.h>

#include <stdexcept>
#include <string>

namespace gwl {

namespace {

// The libcrypto types stay opaque here, as the library's own headers keep them.
struct EvpCipherCtx;
struct EvpCipher;
struct EvpMd;
struct Engine;

// The libcrypto functions these calls use, resolved once from the system's library.
struct LibCrypto {
    EvpCipherCtx* (*cipherCtxNew)();
    void (*cipherCtxFree)(EvpCipherCtx*);
    int (*cipherInit)(EvpCipherCtx*, const EvpCipher*, Engine*, const unsigned char*, const unsigned char*, int);
    int (*cipherUpdate)(EvpCipherCtx*, unsigned char*, int*, const unsigned char*, int);
    int (*cipherFinal)(EvpCipherCtx*, unsigned char*, int*);
    const EvpCipher* (*aes256Cbc)();
    const EvpMd* (*sha256Md)();
    int (*digest)(const void*, std::size_t, unsigned char*, unsigned int*, const EvpMd*, Engine*);
    int (*pbkdf2)(const char*, int, const unsigned char*, int, int, const EvpMd*, int, unsigned char*);
    int (*randBytes)(unsigned char*, int);
};

template <typename Function>
Function symbol(void* library, const char* name) {
    void* found = dlsym(library, name);
    if (!found) {
        throw std::runtime_error(std::string("The system's libcrypto doesn't provide ") + name + ".");
    }
    return reinterpret_cast<Function>(found);
}

LibCrypto load() {
    void* library = dlopen("libcrypto.so.3", RTLD_NOW);
    if (!library) {
        library = dlopen("libcrypto.so", RTLD_NOW);
    }
    if (!library) {
        throw std::runtime_error("Encrypted accounts require OpenSSL (libcrypto.so.3).");
    }
    LibCrypto lib{};
    lib.cipherCtxNew = symbol<decltype(lib.cipherCtxNew)>(library, "EVP_CIPHER_CTX_new");
    lib.cipherCtxFree = symbol<decltype(lib.cipherCtxFree)>(library, "EVP_CIPHER_CTX_free");
    lib.cipherInit = symbol<decltype(lib.cipherInit)>(library, "EVP_CipherInit_ex");
    lib.cipherUpdate = symbol<decltype(lib.cipherUpdate)>(library, "EVP_CipherUpdate");
    lib.cipherFinal = symbol<decltype(lib.cipherFinal)>(library, "EVP_CipherFinal_ex");
    lib.aes256Cbc = symbol<decltype(lib.aes256Cbc)>(library, "EVP_aes_256_cbc");
    lib.sha256Md = symbol<decltype(lib.sha256Md)>(library, "EVP_sha256");
    lib.digest = symbol<decltype(lib.digest)>(library, "EVP_Digest");
    lib.pbkdf2 = symbol<decltype(lib.pbkdf2)>(library, "PKCS5_PBKDF2_HMAC");
    lib.randBytes = symbol<decltype(lib.randBytes)>(library, "RAND_bytes");
    return lib;
}

const LibCrypto& libcrypto() {
    static const LibCrypto lib = load();
    return lib;
}

// Frees a cipher context when it goes out of scope.
class CipherContext {
public:
    CipherContext() : ctx_(libcrypto().cipherCtxNew()) {
        if (!ctx_) {
            throw std::runtime_error("The system's cipher couldn't be started.");
        }
    }
    ~CipherContext() { libcrypto().cipherCtxFree(ctx_); }
    CipherContext(const CipherContext&) = delete;
    CipherContext& operator=(const CipherContext&) = delete;
    EvpCipherCtx* get() const { return ctx_; }

private:
    EvpCipherCtx* ctx_;
};

// Runs AES-256-CBC in the given direction. Returns nothing when the cipher reports an error, such as bad padding.
std::optional<Bytes> runCipher(const Bytes& key, const Bytes& iv, const Bytes& input, bool encrypt) {
    const LibCrypto& lib = libcrypto();
    CipherContext context;
    if (lib.cipherInit(context.get(), lib.aes256Cbc(), nullptr, key.data(), iv.data(), encrypt ? 1 : 0) != 1) {
        return std::nullopt;
    }
    Bytes output(input.size() + 32);
    int written = 0;
    int total = 0;
    if (lib.cipherUpdate(context.get(), output.data(), &written, input.data(), static_cast<int>(input.size())) != 1) {
        return std::nullopt;
    }
    total = written;
    if (lib.cipherFinal(context.get(), output.data() + total, &written) != 1) {
        return std::nullopt;
    }
    total += written;
    output.resize(static_cast<std::size_t>(total));
    return output;
}

}  // namespace

Bytes randomBytes(std::size_t count) {
    Bytes bytes(count);
    if (count > 0 && libcrypto().randBytes(bytes.data(), static_cast<int>(count)) != 1) {
        throw std::runtime_error("The system's random numbers couldn't be read.");
    }
    return bytes;
}

Bytes sha256(const Bytes& data) {
    Bytes hash(32);
    unsigned int length = 0;
    if (libcrypto().digest(data.data(), data.size(), hash.data(), &length, libcrypto().sha256Md(), nullptr) != 1) {
        throw std::runtime_error("The system's SHA-256 isn't available.");
    }
    return hash;
}

Bytes pbkdf2Sha256(const Bytes& password, const Bytes& salt, std::uint32_t iterations, std::size_t length) {
    Bytes key(length);
    const int ok = libcrypto().pbkdf2(reinterpret_cast<const char*>(password.data()), static_cast<int>(password.size()), salt.data(),
                                      static_cast<int>(salt.size()), static_cast<int>(iterations), libcrypto().sha256Md(),
                                      static_cast<int>(length), key.data());
    if (ok != 1) {
        throw std::runtime_error("The system's key derivation isn't available.");
    }
    return key;
}

Bytes aes256CbcEncrypt(const Bytes& key, const Bytes& iv, const Bytes& plain) {
    auto cipher = runCipher(key, iv, plain, true);
    if (!cipher) {
        throw std::runtime_error("The system's cipher couldn't encrypt.");
    }
    return *cipher;
}

std::optional<Bytes> aes256CbcDecrypt(const Bytes& key, const Bytes& iv, const Bytes& cipher) {
    return runCipher(key, iv, cipher, false);
}

}  // namespace gwl
