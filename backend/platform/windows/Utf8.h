#pragma once

#include <windows.h>
#include <string>

namespace gwl {

inline std::wstring toWide(const std::string& text) {
    if (text.empty()) return {};
    const int length = MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0);
    std::wstring wide(length, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), wide.data(), length);
    return wide;
}

inline std::string toUtf8(const wchar_t* text) {
    if (!text) return {};
    // Windows counts the terminating null when the input length is -1.
    const int length = WideCharToMultiByte(CP_UTF8, 0, text, -1, nullptr, 0, nullptr, nullptr);
    std::string utf8(length, '\0');
    if (length) {
        WideCharToMultiByte(CP_UTF8, 0, text, -1, utf8.data(), length, nullptr, nullptr);
        utf8.pop_back();
    }
    return utf8;
}

}  // namespace gwl
