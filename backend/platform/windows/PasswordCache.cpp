#include "PasswordCache.h"

#include <windows.h>
#include <dpapi.h>

#include <algorithm>
#include <array>
#include <string>

#include "utils/Strings.h"

namespace gwl {

namespace {

const char* const kSessionKey = "SOFTWARE\\GW_Launcher\\Session";
const char* const kCachedPassword = "CachedPassword";

// Protects or opens data for this user with DPAPI. Nothing when Windows refuses.
std::optional<Bytes> dpapi(const Bytes& data, bool protect) {
    DATA_BLOB input{static_cast<DWORD>(data.size()), const_cast<BYTE*>(data.data())};
    DATA_BLOB output{};
    const BOOL ok = protect ? CryptProtectData(&input, nullptr, nullptr, nullptr, nullptr, 0, &output)
                            : CryptUnprotectData(&input, nullptr, nullptr, nullptr, nullptr, 0, &output);
    if (!ok) {
        return std::nullopt;
    }
    Bytes result(output.pbData, output.pbData + output.cbData);
    LocalFree(output.pbData);
    return result;
}

}  // namespace

std::optional<PasswordHash> rememberedPassword() {
    HKEY key = nullptr;
    if (RegOpenKeyExA(HKEY_CURRENT_USER, kSessionKey, 0, KEY_READ, &key) != ERROR_SUCCESS) {
        return std::nullopt;
    }
    DWORD type = 0;
    DWORD size = 0;
    std::optional<PasswordHash> result;
    if (RegQueryValueExA(key, kCachedPassword, nullptr, &type, nullptr, &size) == ERROR_SUCCESS && type == REG_SZ) {
        std::string text(size, '\0');
        if (RegQueryValueExA(key, kCachedPassword, nullptr, &type, reinterpret_cast<BYTE*>(text.data()), &size) == ERROR_SUCCESS) {
            text.resize(std::char_traits<char>::length(text.c_str()));
            const auto protectedBytes = base64Decode(text);
            const auto hash = protectedBytes ? dpapi(*protectedBytes, false) : std::nullopt;
            if (hash && hash->size() == std::tuple_size_v<PasswordHash>) {
                PasswordHash value{};
                std::copy(hash->begin(), hash->end(), value.begin());
                result = value;
            }
        }
    }
    RegCloseKey(key);
    return result;
}

void rememberPassword(const PasswordHash& password) {
    const auto protectedHash = dpapi(Bytes(password.begin(), password.end()), true);
    if (!protectedHash) {
        return;
    }
    HKEY key = nullptr;
    if (RegCreateKeyExA(HKEY_CURRENT_USER, kSessionKey, 0, nullptr, 0, KEY_WRITE, nullptr, &key, nullptr) != ERROR_SUCCESS) {
        return;
    }
    const std::string text = base64Encode(*protectedHash);
    RegSetValueExA(key, kCachedPassword, 0, REG_SZ, reinterpret_cast<const BYTE*>(text.c_str()), static_cast<DWORD>(text.size() + 1));
    RegCloseKey(key);
}

void forgetPassword() {
    HKEY key = nullptr;
    if (RegOpenKeyExA(HKEY_CURRENT_USER, kSessionKey, 0, KEY_WRITE, &key) != ERROR_SUCCESS) {
        return;
    }
    RegDeleteValueA(key, kCachedPassword);
    RegCloseKey(key);
}

}  // namespace gwl
