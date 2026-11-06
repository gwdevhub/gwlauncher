// Windows: a launch restarts this executable in CLI mode; Wine's prerequisites don't apply.

#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <system_error>
#include <optional>
#include <stdexcept>
#include <string>

#include <windows.h>

#include "Desktop.h"
#include "Launch.h"
#include "Process.h"
#include "utils/Files.h"

namespace gwl {

namespace {

namespace fs = std::filesystem;

std::string manualSteamPath;

// The Steam client's folder, as Steam records it in the registry when it's installed.
std::optional<fs::path> detectedSteamRoot() {
    wchar_t folder[MAX_PATH] = {};
    DWORD size = sizeof(folder);
    if (RegGetValueW(HKEY_CURRENT_USER, L"Software\\Valve\\Steam", L"SteamPath", RRF_RT_REG_SZ, nullptr, folder, &size) != ERROR_SUCCESS) {
        return std::nullopt;
    }
    return fs::path(folder);
}

// The Steam client's install at a folder: the one with steam.exe in it.
std::optional<fs::path> steamInstallAt(const fs::path& folder) {
    std::error_code error;
    if (!fs::is_regular_file(folder / "steam.exe", error)) {
        return std::nullopt;
    }
    return folder;
}

// The Steam client's install: the manual folder when one is set, else the one Steam recorded.
std::optional<fs::path> steamRoot() {
    if (!manualSteamPath.empty()) {
        return steamInstallAt(pathFromUtf8(manualSteamPath));
    }
    const auto detected = detectedSteamRoot();
    return detected ? steamInstallAt(*detected) : std::nullopt;
}

// The client library, which Steam keeps in its own folder. Its interfaces are read the way the Linux launcher reads them.
void* steamClientInterface() {
    static void* client = nullptr;
    if (client) {
        return client;
    }
    const auto root = steamRoot();
    if (!root) {
        return nullptr;
    }
    // The client library loads its own Steam DLLs from its folder, which the altered search path finds.
    HMODULE library = LoadLibraryExW((*root / "steamclient64.dll").c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    if (!library) {
        return nullptr;
    }
    auto createInterface = reinterpret_cast<void* (*)(const char*, int*)>(GetProcAddress(library, "CreateInterface"));
    if (!createInterface) {
        return nullptr;
    }
    // The app ID is set only while the library loads, so the game this launcher starts doesn't inherit it.
    SetEnvironmentVariableW(L"SteamAppId", L"29720");
    SetEnvironmentVariableW(L"SteamGameId", L"29720");
    client = createInterface("SteamClient023", nullptr);
    SetEnvironmentVariableW(L"SteamAppId", nullptr);
    SetEnvironmentVariableW(L"SteamGameId", nullptr);
    return client;
}

}  // namespace

bool launchesThroughWine() {
    return false;
}

bool steamClientInstalled() {
    return steamRoot().has_value();
}

bool protonInstalled() {
    return false;
}

bool wineRuns() {
    return false;
}

std::optional<std::string> wineInstallCommand() {
    return std::nullopt;
}

// Windows starts accounts directly, so there's no Wine program to find or set.
std::string detectedSteamPath() {
    const auto detected = detectedSteamRoot();
    const auto root = detected ? steamInstallAt(*detected) : std::nullopt;
    return root ? pathToUtf8(*root) : std::string();
}

std::string detectedWinePath() {
    return {};
}

void setInstallPaths(const std::string& steamPath, const std::string&) {
    manualSteamPath = steamPath;
}

std::optional<std::string> invalidInstallPath(const std::string& steamPath, const std::string&) {
    if (!steamPath.empty() && !steamInstallAt(pathFromUtf8(steamPath))) {
        return "There's no Steam client in " + steamPath + ": steam.exe is missing.";
    }
    return std::nullopt;
}

void startSteamClient() {
    const auto root = steamRoot();
    if (!root) {
        throw std::runtime_error("Steam isn't installed: its folder isn't in the registry.");
    }
    runProgramDetached({pathToUtf8(*root / "steam.exe"), "-silent"});
}

// Windows has no pipe to read, so Steam is ready once it runs. The logged-in wait covers the time it takes to come up.
bool steamReady() {
    return steamRunning();
}

bool steamLoggedIn() {
    return steamLoggedInOn(steamClientInterface());
}

void openProtonInstall() {
    throw std::runtime_error("Installing Proton isn't ported to the native Windows build yet.");
}

std::vector<std::filesystem::path> launchWriteFolders(const Account&) {
    return {launcherDataDir()};
}

std::filesystem::path launcherDataDir() {
    wchar_t path[MAX_PATH] = {};
    if (GetModuleFileNameW(nullptr, path, MAX_PATH) == 0) {
        return std::filesystem::path(".");
    }
    return std::filesystem::path(path).parent_path();
}

std::optional<std::string> linuxPathFor(const std::string& windowsPath) {
    return windowsPath;
}

std::filesystem::path defaultGameFolder() {
    const wchar_t* local = _wgetenv(L"LOCALAPPDATA");
    return std::filesystem::path(local ? local : L".") / "GW_Launcher" / "Guild Wars";
}

void setDefaultGame(const std::filesystem::path& exe) {
    // The Guild Wars registry key that a client reads its install from. Both values hold the executable's path.
    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, L"Software\\ArenaNet\\Guild Wars", 0, nullptr, 0, KEY_WRITE, nullptr, &key, nullptr) !=
        ERROR_SUCCESS) {
        throw std::runtime_error("The Guild Wars registry key couldn't be opened.");
    }
    const std::wstring text = exe.wstring();
    const auto size = static_cast<DWORD>((text.size() + 1) * sizeof(wchar_t));
    const bool wrotePath = RegSetValueExW(key, L"Path", 0, REG_SZ, reinterpret_cast<const BYTE*>(text.c_str()), size) == ERROR_SUCCESS;
    const bool wroteSrc = RegSetValueExW(key, L"Src", 0, REG_SZ, reinterpret_cast<const BYTE*>(text.c_str()), size) == ERROR_SUCCESS;
    RegCloseKey(key);
    if (!wrotePath || !wroteSrc) {
        throw std::runtime_error("The default install couldn't be recorded in the registry.");
    }
}

// Writes the loader, which the launcher carries as resource 2, beside the launcher, unless the copy there already matches. It's staged
// and then moved over the old copy, so a copy that's cut short is never loaded.
void installWebView2Loader() {
    const HRSRC resource = FindResourceW(nullptr, MAKEINTRESOURCEW(2), MAKEINTRESOURCEW(10));
    if (!resource) {
        return;
    }
    const std::pair<const unsigned char*, std::size_t> bytes{
        static_cast<const unsigned char*>(LockResource(LoadResource(nullptr, resource))), SizeofResource(nullptr, resource)};
    if (!bytes.first || bytes.second == 0) {
        return;
    }
    const std::filesystem::path folder = launcherDataDir();
    const std::filesystem::path target = folder / "WebView2Loader.dll";
    std::error_code error;
    if (std::filesystem::is_regular_file(target, error) && std::filesystem::file_size(target, error) == bytes.second) {
        std::ifstream existing(target, std::ios::binary);
        const std::string text((std::istreambuf_iterator<char>(existing)), std::istreambuf_iterator<char>());
        if (text.size() == bytes.second && std::memcmp(text.data(), bytes.first, bytes.second) == 0) {
            return;
        }
    }
    const std::filesystem::path staged = folder / "WebView2Loader.dll.new";
    {
        std::ofstream file(staged, std::ios::binary | std::ios::trunc);
        file.write(reinterpret_cast<const char*>(bytes.first), static_cast<std::streamsize>(bytes.second));
        if (!file) {
            throw std::runtime_error("The WebView2 loader couldn't be written to " + pathToUtf8(staged));
        }
    }
    std::filesystem::rename(staged, target, error);
    if (error) {
        std::filesystem::remove(staged, error);
        throw std::runtime_error("Can't replace the WebView2 loader while it's in use: " + pathToUtf8(target));
    }
}

}  // namespace gwl
