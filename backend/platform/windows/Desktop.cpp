// Windows: the desktop facts the launcher checks, the error and file dialogs, and the cursor position.

#include <windows.h>
#include <commdlg.h>
#include <knownfolders.h>
#include <objbase.h>
#include <shlobj.h>

#include <cstdio>
#include <filesystem>
#include <optional>
#include <stdexcept>
#include <string>
#include <utility>

#include "Desktop.h"
#include "Settings.h"
#include "StartupWatch.h"
#include "WebViewWindow.h"
#include "utils/Files.h"
#include "utils/Strings.h"

namespace gwl {

void showError(const std::string& message) {
    std::fprintf(stderr, "%s\n", message.c_str());
    pauseStartupMonitoring(true);
    MessageBoxW(nullptr, toWide(message).c_str(), L"GW Launcher", MB_OK | MB_ICONERROR);
    pauseStartupMonitoring(false);
}

std::optional<std::string> pickFile(const std::string& title, bool modsOnly, const std::string& startFolder) {
    // The filters are pairs of description and pattern, each ended by a NUL, and the list ends with an extra NUL.
    static const wchar_t gwFilter[] = L"Guild Wars or shortcut\0*.exe;*.lnk;*.desktop\0\0";
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
    dialog.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
    if (!GetOpenFileNameW(&dialog)) {
        return std::nullopt;
    }
    return toUtf8(file);
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
            return pathToUtf8(path);
        }
        if (std::filesystem::is_regular_file(path / "Gw.exe")) {
            return pathToUtf8(path / "Gw.exe");
        }
    }
    return std::nullopt;
}

// The screen's size in pixels.
std::optional<std::pair<int, int>> screenSize() {
    return std::make_pair(GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
}

std::filesystem::path webViewDataFolder() {
    PWSTR folder = nullptr;
    if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &folder))) {
        throw std::runtime_error("The local application data folder couldn't be found for WebView2.");
    }
    std::filesystem::path path(folder);
    CoTaskMemFree(folder);
    return path / "GWLauncher" / "WebView2";
}

bool confirmRetry(const std::string& message) {
    const std::wstring text = toWide(message + "\n\nRetry recreates the interface; running jobs are not restarted. Cancel exits GW Launcher.\nPress Ctrl+C to copy this error.");
    pauseStartupMonitoring(true);
    const int answer = MessageBoxW(nullptr, text.c_str(), L"GW Launcher interface error", MB_RETRYCANCEL | MB_ICONERROR);
    pauseStartupMonitoring(false);
    return answer == IDRETRY;
}

}  // namespace gwl

// The desktop function the WebViewWindow library calls. The library has it in the global namespace, and the launcher defines it above
// in gwl, so it forwards to that. The pointer and the work areas come from the library.
void showError(const std::string& message) {
    gwl::showError(message);
}
