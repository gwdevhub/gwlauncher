// Windows: the edition and build from the registry, named the way the Rust launcher names them. Windows 11 still reports itself as
// Windows 10 in its product name, so the name comes from the build number.

#include "SystemInfo.h"

#include <windows.h>

#include <cstdlib>
#include <string>

namespace gwl {

namespace {

// A string value under HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion, or empty when it's missing.
std::string registryString(const wchar_t* name) {
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
        return "";
    }
    wchar_t value[256] = {};
    DWORD bytes = sizeof(value);
    const LONG status = RegQueryValueExW(key, name, nullptr, nullptr, reinterpret_cast<BYTE*>(value), &bytes);
    RegCloseKey(key);
    if (status != ERROR_SUCCESS) {
        return "";
    }
    std::string text;
    for (wchar_t c : std::wstring(value)) {
        text.push_back(static_cast<char>(c));
    }
    return text;
}

std::string trimmed(std::string text) {
    const auto first = text.find_first_not_of(' ');
    if (first == std::string::npos) {
        return "";
    }
    return text.substr(first, text.find_last_not_of(' ') - first + 1);
}

}  // namespace

std::string osDescription() {
    const std::string build = registryString(L"CurrentBuild");
    const std::string product = registryString(L"ProductName");
    std::string edition = product;
    for (const std::string prefix : {"Windows 10", "Windows 11"}) {
        if (edition.rfind(prefix, 0) == 0) {
            edition = edition.substr(prefix.size());
        }
    }
    const bool windows11 = !build.empty() && std::strtoul(build.c_str(), nullptr, 10) >= 22000;
    const std::string description = std::string(windows11 ? "Windows 11" : "Windows 10") + " " + trimmed(edition) + " " + build;
    // Single spaces only, as the Rust launcher's description has them when the edition or build is missing.
    std::string out;
    for (char c : description) {
        if (c != ' ' || (!out.empty() && out.back() != ' ')) {
            out.push_back(c);
        }
    }
    return trimmed(out);
}

}  // namespace gwl
