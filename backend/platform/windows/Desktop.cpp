// Windows: the desktop facts the launcher checks, the error and file dialogs, and the cursor position.

#include <windows.h>
#include <commdlg.h>

#include <cstdio>
#include <filesystem>
#include <optional>
#include <string>
#include <utility>

#include "Settings.h"
#include "WebViewWindow.h"

namespace gwl {

namespace {

std::wstring toWide(const std::string& text) {
    const int length = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, nullptr, 0);
    std::wstring wide(length > 0 ? length : 1, L'\0');
    if (length > 0) {
        MultiByteToWideChar(CP_UTF8, 0, text.c_str(), -1, wide.data(), length);
    }
    return wide;
}

std::string toUtf8(const wchar_t* text) {
    const int length = WideCharToMultiByte(CP_UTF8, 0, text, -1, nullptr, 0, nullptr, nullptr);
    std::string utf8(length > 0 ? length - 1 : 0, '\0');
    if (length > 1) {
        WideCharToMultiByte(CP_UTF8, 0, text, -1, utf8.data(), length, nullptr, nullptr);
    }
    return utf8;
}

}  // namespace

void showError(const std::string& message) {
    std::fprintf(stderr, "%s\n", message.c_str());
    MessageBoxW(nullptr, toWide(message).c_str(), L"GW Launcher", MB_OK | MB_ICONERROR);
}

std::optional<std::string> pickFile(const std::string& title, bool modsOnly, const std::string& startFolder) {
    // The filters are pairs of description and pattern, each ended by a NUL, and the list ends with an extra NUL.
    static const wchar_t gwFilter[] = L"Guild Wars\0Gw.exe\0\0";
    static const wchar_t modFilter[] = L"Mods\0*.dll;*.tpf;*.zip\0\0";
    const std::wstring wideTitle = toWide(title);
    const std::wstring wideFolder = toWide(startFolder);
    wchar_t file[1024] = {};
    OPENFILENAMEW dialog{};
    dialog.lStructSize = sizeof(dialog);
    dialog.lpstrFilter = modsOnly ? modFilter : gwFilter;
    dialog.lpstrFile = file;
    dialog.nMaxFile = 1024;
    dialog.lpstrTitle = wideTitle.c_str();
    dialog.lpstrInitialDir = startFolder.empty() ? nullptr : wideFolder.c_str();
    dialog.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST;
    if (!GetOpenFileNameW(&dialog)) {
        return std::nullopt;
    }
    return toUtf8(file);
}

std::optional<std::pair<int, int>> cursorPosition() {
    POINT point{};
    if (!GetCursorPos(&point)) {
        return std::nullopt;
    }
    return std::make_pair(static_cast<int>(point.x), static_cast<int>(point.y));
}

// The game's folder from the ArenaNet install keys, the user's first and then the installer's. A value is a path to
// Gw.exe, or to a folder that holds one, and only one that exists counts.
std::optional<std::string> defaultInstall() {
    struct Key {
        HKEY root;
        const wchar_t* path;
    };
    const Key keys[] = {
        {HKEY_CURRENT_USER, L"Software\\ArenaNet\\Guild Wars"},
        {HKEY_LOCAL_MACHINE, L"SOFTWARE\\ArenaNet\\Guild Wars"},
        {HKEY_LOCAL_MACHINE, L"SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars"},
    };
    for (const Key& key : keys) {
        HKEY opened = nullptr;
        if (RegOpenKeyExW(key.root, key.path, 0, KEY_READ, &opened) != ERROR_SUCCESS) {
            continue;
        }
        wchar_t value[1024] = {};
        DWORD bytes = sizeof(value);
        DWORD type = 0;
        const LONG status = RegQueryValueExW(opened, L"Path", nullptr, &type, reinterpret_cast<LPBYTE>(value), &bytes);
        RegCloseKey(opened);
        if (status != ERROR_SUCCESS || type != REG_SZ || value[0] == L'\0') {
            continue;
        }
        const std::filesystem::path path(value);
        if (std::filesystem::is_regular_file(path)) {
            return path.string();
        }
        if (std::filesystem::is_regular_file(path / "Gw.exe")) {
            return (path / "Gw.exe").string();
        }
    }
    return std::nullopt;
}

// The screen's size in pixels.
std::optional<std::pair<int, int>> screenSize() {
    return std::make_pair(GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
}

// The work area: the screen less the taskbar.
std::optional<Rect> workArea() {
    RECT area{};
    if (!SystemParametersInfoW(SPI_GETWORKAREA, 0, &area, 0)) {
        return std::nullopt;
    }
    return Rect{area.left, area.top, area.right - area.left, area.bottom - area.top};
}

// The work area of the monitor that holds most of the rectangle, or the nearest one when it's off every monitor.
std::optional<Rect> workAreaAt(const Rect& rect) {
    const RECT bounds{rect.x, rect.y, rect.right(), rect.bottom()};
    MONITORINFO info{};
    info.cbSize = sizeof(info);
    if (!GetMonitorInfoW(MonitorFromRect(&bounds, MONITOR_DEFAULTTONEAREST), &info)) {
        return std::nullopt;
    }
    const RECT& area = info.rcWork;
    return Rect{area.left, area.top, area.right - area.left, area.bottom - area.top};
}

}  // namespace gwl
