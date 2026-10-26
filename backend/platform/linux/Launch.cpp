// Linux: Guild Wars runs under Wine, the system's or Steam's Proton. The launcher's Windows helper is run under it with
// --launch, which starts the game the same way the Windows launcher does, mods included.

#include "Progress.h"
#include "EmbeddedCli.h"
#include "Launch.h"
#include "Shortcuts.h"
#include "utils/Strings.h"

#include <dlfcn.h>
#include <fcntl.h>
#include <signal.h>
#include <spawn.h>
#include <sys/stat.h>
#include <sys/wait.h>
#include <unistd.h>

#include <algorithm>
#include <cctype>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <initializer_list>
#include <ctime>
#include <fstream>
#include <sstream>
#include <stdexcept>
#include <thread>
#include <utility>
#include <vector>

extern char** environ;

namespace gwl {

namespace {

namespace fs = std::filesystem;

using Environment = std::vector<std::pair<std::string, std::string>>;

// The paths set by hand in Settings.json. Empty means they're found.
std::string manualSteamPath;
std::string manualWinePath;

const auto kLaunchLimit = std::chrono::seconds(120);
const char* const kSteamAppId = "29720";
const char* const kDxvkOverrides = "mscoree,mshtml=;d3d9,d3d11,d3d10core,dxgi=n";

// The Steam client's files Proton copies from its legacycompat folder, with their names in the prefix.
const std::pair<const char*, const char*> kSteamClientFiles[] = {
    {"steamclient.dll", "steamclient.dll"},
    {"steamclient64.dll", "steamclient64.dll"},
    {"GameOverlayRenderer64.dll", "GameOverlayRenderer64.dll"},
    {"SteamService.exe", "steam.exe"},
    {"Steam.dll", "Steam.dll"},
};
const char* const kSteamInPrefix = "drive_c/Program Files (x86)/Steam";
const char* const kSteamWindowsDir = "C:\\Program Files (x86)\\Steam";
const char* const kDxvkDlls[] = {"d3d9", "d3d11", "d3d10core", "dxgi"};

// A Wine to run: its binary, the prefix it uses, the variables it needs, and the Proton libraries holding DXVK.
struct Runtime {
    fs::path wine;
    fs::path prefix;
    Environment env;
    std::optional<fs::path> libDir;
};

std::string envOr(const char* name, const std::string& fallback) {
    const char* value = std::getenv(name);
    return value && *value ? value : fallback;
}

fs::path homeDir() {
    const char* home = std::getenv("HOME");
    return home ? fs::path(home) : fs::path(".");
}

// Spawns a program with the launcher's environment, plus overrides, and returns its process ID.
pid_t spawnProgram(const std::vector<std::string>& argv, const Environment& overrides, posix_spawn_file_actions_t* actions,
                   const std::string& cwd = {}) {
    Environment env;
    for (char** entry = environ; *entry; ++entry) {
        std::string text = *entry;
        const std::string key = text.substr(0, text.find('='));
        const bool replaced = std::any_of(overrides.begin(), overrides.end(), [&](const auto& pair) { return pair.first == key; });
        if (!replaced) {
            env.emplace_back(key, text.substr(key.size() + 1));
        }
    }
    env.insert(env.end(), overrides.begin(), overrides.end());
    std::vector<std::string> envText;
    for (const auto& [key, value] : env) {
        envText.push_back(key + "=" + value);
    }
    std::vector<char*> envp;
    for (auto& text : envText) {
        envp.push_back(text.data());
    }
    envp.push_back(nullptr);
    std::vector<char*> args;
    for (const auto& arg : argv) {
        args.push_back(const_cast<char*>(arg.c_str()));
    }
    args.push_back(nullptr);

    posix_spawn_file_actions_t localActions;
    if (!actions) {
        posix_spawn_file_actions_init(&localActions);
        actions = &localActions;
    }
    if (!cwd.empty()) {
        posix_spawn_file_actions_addchdir_np(actions, cwd.c_str());
    }
    pid_t pid = 0;
    const int status = posix_spawnp(&pid, argv[0].c_str(), actions, nullptr, args.data(), envp.data());
    if (actions == &localActions) {
        posix_spawn_file_actions_destroy(&localActions);
    }
    if (status != 0) {
        throw std::runtime_error(argv[0] + " could not be started: " + std::strerror(status));
    }
    return pid;
}

// Runs a program to its end, with nothing on stdin or stdout, and returns its exit status and what it wrote to stderr.
std::pair<int, std::string> runCapturingStderr(const std::vector<std::string>& argv, const Environment& overrides) {
    int pipefd[2];
    if (pipe(pipefd) != 0) {
        throw std::runtime_error("A pipe couldn't be made.");
    }
    posix_spawn_file_actions_t actions;
    posix_spawn_file_actions_init(&actions);
    posix_spawn_file_actions_addopen(&actions, 0, "/dev/null", O_RDONLY, 0);
    posix_spawn_file_actions_addopen(&actions, 1, "/dev/null", O_WRONLY, 0);
    posix_spawn_file_actions_adddup2(&actions, pipefd[1], 2);
    posix_spawn_file_actions_addclose(&actions, pipefd[0]);
    posix_spawn_file_actions_addclose(&actions, pipefd[1]);
    pid_t pid = 0;
    try {
        pid = spawnProgram(argv, overrides, &actions);
    } catch (...) {
        posix_spawn_file_actions_destroy(&actions);
        close(pipefd[0]);
        close(pipefd[1]);
        throw;
    }
    posix_spawn_file_actions_destroy(&actions);
    close(pipefd[1]);
    std::string output;
    char buffer[4096];
    ssize_t count = 0;
    while ((count = read(pipefd[0], buffer, sizeof(buffer))) > 0) {
        output.append(buffer, static_cast<std::size_t>(count));
    }
    close(pipefd[0]);
    int status = 0;
    waitpid(pid, &status, 0);
    return {WIFEXITED(status) ? WEXITSTATUS(status) : -1, output};
}

// Whether the Wine binary runs: `wine --version` exits cleanly.
bool runs(const fs::path& wine) {
    try {
        return runCapturingStderr({wine.string(), "--version"}, {}).first == 0;
    } catch (const std::exception&) {
        return false;
    }
}

// The Windows path in the form Wine's Windows side reads: Linux paths become Z: paths.
std::string wineForm(const std::string& path) {
    if (path.empty() || path[0] != '/') {
        return path;
    }
    std::string windows = "Z:" + path;
    std::replace(windows.begin(), windows.end(), '/', '\\');
    return windows;
}

// The Linux folder a drive letter's files are in: Z: is the root, and other letters are the mounts named for them.
std::optional<fs::path> driveRoot(char letter) {
    if (letter == 'Z') {
        return fs::path("/");
    }
    const char* user = std::getenv("USER");
    const std::vector<fs::path> parents{fs::path("/media") / (user ? user : ""), fs::path("/mnt")};
    const char lower = static_cast<char>(std::tolower(static_cast<unsigned char>(letter)));
    for (const auto& parent : parents) {
        std::error_code error;
        for (const auto& entry : fs::directory_iterator(parent, error)) {
            const std::string name = entry.path().filename().string();
            if ((name.size() == 1 && name[0] == lower) || (name.size() > 1 && name[0] == lower && name[1] == '-')) {
                return entry.path();
            }
        }
    }
    return std::nullopt;
}

// The Steam client's install at a folder: the one with its client files in legacycompat.
std::optional<fs::path> steamInstallAt(const fs::path& folder) {
    if (!fs::is_directory(folder / "legacycompat")) {
        return std::nullopt;
    }
    std::error_code error;
    const fs::path canonical = fs::canonical(folder, error);
    return error ? folder : canonical;
}

// The Steam client's install the launcher finds on its own.
std::optional<fs::path> detectedSteamRoot() {
    const fs::path home = homeDir();
    for (const fs::path& candidate : {home / ".steam" / "steam", home / ".local" / "share" / "Steam"}) {
        if (const auto root = steamInstallAt(candidate)) {
            return root;
        }
    }
    return std::nullopt;
}

// The Steam client's install: the manual folder when one is set, else the one found on its own.
std::optional<fs::path> steamRoot() {
    return manualSteamPath.empty() ? detectedSteamRoot() : steamInstallAt(manualSteamPath);
}

// The files folder of Steam's Proton: the newest Proton Experimental, or else any Proton, in Steam's library folders.
std::optional<fs::path> steamProtonFiles(const fs::path& root) {
    std::vector<fs::path> libraries{root};
    std::ifstream vdf(root / "steamapps" / "libraryfolders.vdf");
    std::string line;
    while (std::getline(vdf, line)) {
        const auto start = line.find_first_not_of(" \t");
        if (start == std::string::npos || line.compare(start, 6, "\"path\"") != 0) {
            continue;
        }
        const auto open = line.find('"', start + 6);
        const auto close = open == std::string::npos ? std::string::npos : line.find('"', open + 1);
        if (close != std::string::npos) {
            libraries.emplace_back(line.substr(open + 1, close - open - 1));
        }
    }
    std::vector<fs::path> builds;
    for (const fs::path& library : libraries) {
        std::error_code error;
        for (const auto& entry : fs::directory_iterator(library / "steamapps" / "common", error)) {
            const std::string name = entry.path().filename().string();
            if (name.rfind("Proton", 0) == 0 && fs::is_regular_file(entry.path() / "files" / "bin" / "wine")) {
                builds.push_back(entry.path());
            }
        }
    }
    if (builds.empty()) {
        return std::nullopt;
    }
    std::stable_sort(builds.begin(), builds.end(), [](const fs::path& a, const fs::path& b) {
        return (a.string().find("Experimental") != std::string::npos) > (b.string().find("Experimental") != std::string::npos);
    });
    return builds.front() / "files";
}

// A program found by its path, or on PATH when the name has no folder. Empty when there's none.
fs::path findProgram(const std::string& name) {
    std::vector<fs::path> candidates;
    if (name.find('/') != std::string::npos) {
        candidates.emplace_back(name);
    } else {
        const char* pathVariable = std::getenv("PATH");
        std::istringstream directories(pathVariable ? pathVariable : "");
        std::string directory;
        while (std::getline(directories, directory, ':')) {
            candidates.push_back(fs::path(directory) / name);
        }
    }
    for (const fs::path& candidate : candidates) {
        std::error_code error;
        if (fs::is_regular_file(candidate, error) && access(candidate.c_str(), X_OK) == 0) {
            return candidate;
        }
    }
    return {};
}

// The Wine program for accounts that aren't Steam accounts: the manual one when set, else GWLAUNCHER_WINE, else wine on PATH.
fs::path wineCommand() {
    return manualWinePath.empty() ? fs::path(envOr("GWLAUNCHER_WINE", "wine")) : fs::path(manualWinePath);
}

// The Wine for accounts that aren't Steam accounts: the system's when it runs, otherwise Steam's Proton.
Runtime systemRuntime(const fs::path& dataDir) {
    const fs::path system = wineCommand();
    if (runs(system)) {
        return Runtime{system, dataDir / "prefix", {}, std::nullopt};
    }
    if (const auto root = steamRoot()) {
        if (const auto files = steamProtonFiles(*root)) {
            const fs::path wine = *files / "bin" / "wine";
            if (runs(wine)) {
                return Runtime{wine, dataDir / "proton-prefix", {{"WINEDLLPATH", (*files / "lib" / "wine").string()},
                                                                 {"WINEDLLOVERRIDES", kDxvkOverrides}}, *files / "lib" / "wine"};
            }
        }
    }
    return Runtime{system, dataDir / "prefix", {}, std::nullopt};
}

// Steam's Proton, with its own prefix and DXVK, which Steam accounts run under.
Runtime steamRuntime(const fs::path& dataDir) {
    const auto root = steamRoot();
    const auto files = root ? steamProtonFiles(*root) : std::nullopt;
    if (!files) {
        throw std::runtime_error("Steam's Proton isn't installed.");
    }
    const fs::path lib = *files / "lib" / "wine";
    return Runtime{*files / "bin" / "wine", dataDir / "steam-prefix", {{"WINEDLLPATH", lib.string()},
                                                                       {"WINEDLLOVERRIDES", kDxvkOverrides}}, lib};
}

// Sets one registry value in the runtime's prefix.
void setValue(const Runtime& runtime, const std::string& key, const std::string& name, const std::string& kind, const std::string& data) {
    Environment overrides = runtime.env;
    overrides.emplace_back("WINEPREFIX", runtime.prefix.string());
    const auto [status, error] = runCapturingStderr({runtime.wine.string(), "reg", "add", key, "/v", name, "/t", kind, "/d", data, "/f"},
                                                    overrides);
    if (status != 0) {
        throw std::runtime_error("Could not set " + name + " in the registry: " + error);
    }
}

// Copies a Steam file into the prefix when the prefix's copy is missing, a different size, or older than Steam's.
void copyIfChanged(const fs::path& from, const fs::path& to) {
    std::error_code error;
    const auto source = fs::status(from, error);
    if (error || !fs::is_regular_file(source)) {
        throw std::runtime_error("Steam's " + from.string() + " couldn't be read.");
    }
    const bool stale = !fs::exists(to) || fs::file_size(from) != fs::file_size(to) || fs::last_write_time(from) > fs::last_write_time(to);
    if (stale) {
        fs::copy_file(from, to, fs::copy_options::overwrite_existing);
    }
}

// Copies Steam's client files into a Proton prefix, where the Steam client's DLLs are looked for. Every Proton prefix gets them.
void copySteamClientFiles(const Runtime& runtime, const fs::path& root) {
    const fs::path target = runtime.prefix / kSteamInPrefix;
    fs::create_directories(target);
    for (const auto& [source, name] : kSteamClientFiles) {
        copyIfChanged(root / "legacycompat" / source, target / name);
    }
}

// Installs DXVK into a Proton prefix. It goes in after any registry writes, which boot the prefix and would otherwise put Wine's own
// DLLs back.
void installDxvk(const Runtime& runtime) {
    const std::pair<const char*, const char*> folders[] = {{"x86_64-windows", "drive_c/windows/system32"},
                                                           {"i386-windows", "drive_c/windows/syswow64"}};
    for (const auto& [arch, folder] : folders) {
        fs::create_directories(runtime.prefix / folder);
        for (const char* dll : kDxvkDlls) {
            copyIfChanged(*runtime.libDir / "dxvk" / arch / (std::string(dll) + ".dll"), runtime.prefix / folder / (std::string(dll) + ".dll"));
        }
    }
}

// The registry values a Steam account's game reads, so it believes Steam is running. Steam accounts only.
Environment steamSetup(const Runtime& runtime, const fs::path& root) {
    std::ifstream pidFile(homeDir() / ".steam" / "steam.pid");
    std::string pid;
    pidFile >> pid;
    if (pid.empty()) {
        throw std::runtime_error("Steam's process ID isn't known.");
    }
    const std::string steamExe = std::string(kSteamWindowsDir) + "\\steam.exe";
    const std::string client = std::string(kSteamWindowsDir) + "\\steamclient.dll";
    const std::string client64 = std::string(kSteamWindowsDir) + "\\steamclient64.dll";
    const std::string appKey = std::string("HKCU\\Software\\Valve\\Steam\\Apps\\") + kSteamAppId;
    setValue(runtime, "HKLM\\Software\\Wow6432Node\\Valve\\Steam", "InstallPath", "REG_SZ", kSteamWindowsDir);
    setValue(runtime, "HKCU\\Software\\Valve\\Steam", "SteamPath", "REG_SZ", kSteamWindowsDir);
    setValue(runtime, "HKCU\\Software\\Valve\\Steam", "SteamExe", "REG_SZ", steamExe);
    setValue(runtime, "HKCU\\Software\\Valve\\Steam\\ActiveProcess", "PID", "REG_DWORD", pid);
    setValue(runtime, "HKCU\\Software\\Valve\\Steam\\ActiveProcess", "SteamClientDll", "REG_SZ", client);
    setValue(runtime, "HKCU\\Software\\Valve\\Steam\\ActiveProcess", "SteamClientDll64", "REG_SZ", client64);
    setValue(runtime, "HKCU\\Software\\Valve\\Steam\\ActiveProcess", "SteamPath", "REG_SZ", kSteamWindowsDir);
    setValue(runtime, appKey, "Installed", "REG_DWORD", "1");
    setValue(runtime, appKey, "Running", "REG_DWORD", "1");

    return {{"SteamAppId", kSteamAppId}, {"SteamGameId", kSteamAppId}, {"STEAM_COMPAT_CLIENT_INSTALL_PATH", root.string()}};
}

// Points the prefix's game registry at the game, the way the Windows launcher does before a start.
void setGamePath(const Runtime& runtime, const std::string& linuxExe) {
    fs::create_directories(runtime.prefix);
    setValue(runtime, "HKCU\\Software\\ArenaNet\\Guild Wars", "Path", "REG_SZ", wineForm(linuxExe));
    setValue(runtime, "HKCU\\Software\\ArenaNet\\Guild Wars", "Src", "REG_SZ", wineForm(linuxExe));
}

// The command-line launcher that runs the game under Wine: the copy in the data folder, which is written from the one this program
// carries. GWLAUNCHER_HELPER names another copy to use instead, for testing.
fs::path helperPath() {
    const std::string override = envOr("GWLAUNCHER_HELPER", "");
    if (!override.empty() && fs::is_regular_file(override)) {
        return override;
    }
    return installEmbeddedCli();
}

// The reason the helper gave, without Wine's debug lines, or a generic one when it printed nothing.
std::string failureReason(const std::string& stderrText) {
    std::istringstream lines(stderrText);
    std::string line;
    std::string reason;
    while (std::getline(lines, line)) {
        const auto start = line.find_first_not_of(" \t\r");
        if (start == std::string::npos) {
            continue;
        }
        const std::string trimmed = line.substr(start, line.find_last_not_of(" \t\r") - start + 1);
        if (trimmed.find(":err:") == std::string::npos && trimmed.find(":fixme:") == std::string::npos && trimmed.rfind("wine:", 0) != 0) {
            reason = trimmed;
        }
    }
    return reason.empty() ? "The game didn't start cleanly. Check the account's mods and game path." : reason;
}

// Waits for a child, killing it when it runs past the limit. Returns its exit status, or -1 when it was killed.
int waitFor(pid_t pid) {
    const auto started = std::chrono::steady_clock::now();
    while (true) {
        int status = 0;
        const pid_t done = waitpid(pid, &status, WNOHANG);
        if (done == pid) {
            return WIFEXITED(status) ? WEXITSTATUS(status) : -1;
        }
        if (std::chrono::steady_clock::now() - started > kLaunchLimit) {
            kill(pid, SIGKILL);
            waitpid(pid, &status, 0);
            return -1;
        }
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
    }
}

}  // namespace

std::vector<fs::path> launchWriteFolders(const Account& account) {
    const fs::path dataDir = launcherDataDir();
    const Runtime runtime = account.isSteamAccount ? steamRuntime(dataDir) : systemRuntime(dataDir);
    // The launch file and its error file go in the current folder, which the command-line launcher runs from.
    return {fs::current_path(), runtime.prefix};
}

fs::path launcherDataDir() {
    const std::string xdg = envOr("XDG_DATA_HOME", "");
    const fs::path base = !xdg.empty() && xdg[0] == '/' ? fs::path(xdg) : homeDir() / ".local" / "share";
    return base / "gwlauncher";
}

std::optional<std::string> linuxPathFor(const std::string& windowsPath) {
    if (windowsPath.size() < 3 || windowsPath[1] != ':') {
        return windowsPath;
    }
    std::string rest = windowsPath.substr(2);
    std::replace(rest.begin(), rest.end(), '\\', '/');
    const char letter = static_cast<char>(std::toupper(static_cast<unsigned char>(windowsPath[0])));
    const auto root = driveRoot(letter);
    if (!root) {
        return std::nullopt;
    }
    const fs::path candidate = *root / rest.substr(rest.find_first_not_of('/'));
    return candidate.string();
}

void startGame(const Account& original, const std::string& dir, std::uint32_t timeoutMs, const std::function<bool()>& confirm, bool requireSteam) {
    const auto account = resolveAccountShortcut(original);
    (void)requireSteam;
    const std::string exe = account.gwpath;
    if (!fs::is_regular_file(exe)) {
        throw std::runtime_error("Invalid path to Gw.exe.");
    }
    const fs::path dataDir = launcherDataDir();
    const Runtime runtime = account.isSteamAccount ? steamRuntime(dataDir) : systemRuntime(dataDir);
    if (!runs(runtime.wine)) {
        throw std::runtime_error(account.isSteamAccount ? "Steam's Proton Wine couldn't start." : "Wine isn't installed.");
    }
    const fs::path helper = helperPath();

    Environment overrides = runtime.env;
    overrides.emplace_back("WINEPREFIX", runtime.prefix.string());
    // A Proton prefix gets Steam's client files and DXVK. A system Wine gets nothing, so a Wine without Direct3D 9 stops the launch
    // with the missing DLL.
    if (runtime.libDir) {
        const auto root = steamRoot();
        if (!root) {
            throw std::runtime_error("Steam's client files aren't installed.");
        }
        copySteamClientFiles(runtime, *root);
        if (account.isSteamAccount) {
            setGamePath(runtime, exe);
            for (const auto& pair : steamSetup(runtime, *root)) {
                overrides.push_back(pair);
            }
        }
        installDxvk(runtime);
    }

    // The helper reads the account in the same form as Accounts.json, with its paths in Wine's form.
    Json file = account.fileJson();
    file["gwpath"] = wineForm(file["gwpath"].get<std::string>());
    for (auto& mod : file["mods"]) {
        mod["fileName"] = wineForm(mod["fileName"].get<std::string>());
    }
    const auto stamp = std::chrono::steady_clock::now().time_since_epoch().count();
    const std::string name = "launch-account-" + std::to_string(getpid()) + "-" + std::to_string(stamp) + ".json";
    const fs::path accountFile = fs::path(dir) / name;
    std::ofstream(accountFile) << file.dump();
    const fs::path errorFile = fs::path(dir) / (name + ".errors");

    struct Cleanup {
        fs::path accountFile;
        fs::path errorFile;
        ~Cleanup() {
            std::error_code error;
            fs::remove(accountFile, error);
            fs::remove(errorFile, error);
        }
    } cleanup{accountFile, errorFile};

    std::vector<std::string> argv{runtime.wine.string(), helper.string(), "--launch", name, std::to_string(timeoutMs)};
    if (confirm) {
        argv.push_back("--confirm");
    }

    // Stdin and stdout are pipes for the resume prompt. Stderr goes to a file, since the game inherits the helper's handles.
    int toChild[2] = {-1, -1};
    int fromChild[2] = {-1, -1};
    if (confirm && (pipe(toChild) != 0 || pipe(fromChild) != 0)) {
        throw std::runtime_error("A pipe couldn't be made.");
    }
    posix_spawn_file_actions_t actions;
    posix_spawn_file_actions_init(&actions);
    if (confirm) {
        posix_spawn_file_actions_adddup2(&actions, toChild[0], 0);
        posix_spawn_file_actions_adddup2(&actions, fromChild[1], 1);
        posix_spawn_file_actions_addclose(&actions, toChild[1]);
        posix_spawn_file_actions_addclose(&actions, fromChild[0]);
    } else {
        posix_spawn_file_actions_addopen(&actions, 0, "/dev/null", O_RDONLY, 0);
        posix_spawn_file_actions_addopen(&actions, 1, "/dev/null", O_WRONLY, 0);
    }
    posix_spawn_file_actions_addopen(&actions, 2, errorFile.c_str(), O_WRONLY | O_CREAT | O_TRUNC, 0644);

    pid_t pid = 0;
    try {
        pid = spawnProgram(argv, overrides, &actions, dir);
    } catch (...) {
        posix_spawn_file_actions_destroy(&actions);
        throw;
    }
    posix_spawn_file_actions_destroy(&actions);
    if (confirm) {
        close(toChild[0]);
        close(fromChild[1]);
    }

    if (!confirm) {
        const int status = waitFor(pid);
        if (status != 0) {
            std::ifstream text(errorFile);
            std::stringstream buffer;
            buffer << text.rdbuf();
            throw std::runtime_error(failureReason(buffer.str()));
        }
        return;
    }

    // The helper prints "suspended" once the game waits. The answer goes back on stdin: resume, or cancel.
    std::string first;
    {
        char c = 0;
        while (read(fromChild[0], &c, 1) == 1 && c != '\n') {
            first += c;
        }
    }
    close(fromChild[0]);
    if (first != "suspended") {
        close(toChild[1]);
        waitFor(pid);
        std::ifstream text(errorFile);
        std::stringstream buffer;
        buffer << text.rdbuf();
        throw std::runtime_error(failureReason(buffer.str()));
    }
    const bool resume = confirm();
    const std::string answer = resume ? "resume\n" : "cancel\n";
    if (write(toChild[1], answer.data(), answer.size()) < 0) {
        // The helper has gone; the wait below reports how it ended.
    }
    close(toChild[1]);
    if (!resume) {
        waitFor(pid);
        throw std::runtime_error(kCancelledMessage);
    }
    const int status = waitFor(pid);
    if (status != 0) {
        std::ifstream text(errorFile);
        std::stringstream buffer;
        buffer << text.rdbuf();
        throw std::runtime_error(failureReason(buffer.str()));
    }
}

std::set<std::string> runningGamePaths() {
    std::set<std::string> running;
    std::error_code error;
    for (const auto& entry : fs::directory_iterator("/proc", error)) {
        std::ifstream cmdline(entry.path() / "cmdline", std::ios::binary);
        std::string argument;
        while (std::getline(cmdline, argument, '\0')) {
            const std::string lower = lowerCase(argument);
            if (lower.size() < 6 || lower.compare(lower.size() - 6, 6, "gw.exe") != 0) {
                continue;
            }
            const auto path = argument[0] == '/' ? std::optional<std::string>(argument) : linuxPathFor(argument);
            if (path) {
                const std::string key = lowerCase(*path);
                running.insert(key);
            }
        }
    }
    return running;
}

// The client library, in the folders the steam script uses, then Steam's own install folder.
std::optional<fs::path> steamClientLibrary() {
    std::vector<fs::path> roots;
    for (const char* link : {".steam/steam", ".steam/root"}) {
        std::error_code error;
        const fs::path target = fs::canonical(homeDir() / link, error);
        if (!error) {
            roots.push_back(target);
        }
    }
    roots.push_back(homeDir() / ".steam" / "debian-installation");
    roots.push_back(homeDir() / ".local" / "share" / "Steam");
    for (const fs::path& root : roots) {
        if (fs::is_regular_file(root / "linux64" / "steamclient.so")) {
            return root / "linux64" / "steamclient.so";
        }
    }
    return std::nullopt;
}

// The Steam client interface, loaded once the library is there. The app ID is set only while the library loads, so games don't inherit it.
void* steamClientInterface() {
    static void* client = nullptr;
    if (client) {
        return client;
    }
    const auto library = steamClientLibrary();
    if (!library) {
        return nullptr;
    }
    void* handle = dlopen(library->c_str(), RTLD_NOW | RTLD_LOCAL);
    if (!handle) {
        return nullptr;
    }
    auto createInterface = reinterpret_cast<void* (*)(const char*, int*)>(dlsym(handle, "CreateInterface"));
    if (!createInterface) {
        return nullptr;
    }
    setenv("SteamAppId", "29720", 1);
    setenv("SteamGameId", "29720", 1);
    client = createInterface("SteamClient023", nullptr);
    unsetenv("SteamAppId");
    unsetenv("SteamGameId");
    return client;
}

bool steamLoggedIn() {
    return steamLoggedInOn(steamClientInterface());
}

bool steamRunning() {
    std::error_code error;
    for (const auto& entry : fs::directory_iterator("/proc", error)) {
        std::ifstream comm(entry.path() / "comm");
        std::string name;
        std::getline(comm, name);
        if (name == "steam") {
            return true;
        }
    }
    return false;
}


bool steamClientInstalled() {
    return steamRoot().has_value();
}

bool protonInstalled() {
    const auto root = steamRoot();
    return root && steamProtonFiles(*root).has_value();
}

bool launchesThroughWine() {
    return true;
}

bool wineRuns() {
    return runs(systemRuntime(launcherDataDir()).wine);
}

// Runs a program in the background, reaping it when it ends so it doesn't linger as a zombie.
void runDetached(const std::vector<std::string>& argv, const std::string& failure) {
    posix_spawn_file_actions_t actions;
    posix_spawn_file_actions_init(&actions);
    posix_spawn_file_actions_addopen(&actions, 0, "/dev/null", O_RDONLY, 0);
    posix_spawn_file_actions_addopen(&actions, 1, "/dev/null", O_WRONLY, 0);
    posix_spawn_file_actions_addopen(&actions, 2, "/dev/null", O_WRONLY, 0);
    pid_t pid = 0;
    try {
        pid = spawnProgram(argv, {}, &actions);
    } catch (const std::exception&) {
        posix_spawn_file_actions_destroy(&actions);
        throw std::runtime_error(failure);
    }
    posix_spawn_file_actions_destroy(&actions);
    std::thread([pid] {
        int status = 0;
        waitpid(pid, &status, 0);
    }).detach();
}

// The steam command on the PATH, the way `which steam` finds it.
std::optional<fs::path> steamCommand() {
    const std::string path = envOr("PATH", "");
    std::istringstream folders(path);
    std::string folder;
    while (std::getline(folders, folder, ':')) {
        const fs::path candidate = fs::path(folder.empty() ? "." : folder) / "steam";
        std::error_code error;
        if (fs::is_regular_file(candidate, error) && ::access(candidate.c_str(), X_OK) == 0) {
            return candidate;
        }
    }
    return std::nullopt;
}

void startSteamClient() {
    const auto steam = steamCommand();
    if (!steam) {
        throw std::runtime_error("Steam isn't installed: the steam command isn't on the PATH.");
    }
    runDetached({steam->string(), "-silent"}, "Steam could not be started.");
}

bool steamReady() {
    // Steam's pipe is a FIFO that stays on disk after Steam closes, so it's the open that says whether Steam is reading it. Opening
    // it for writing without blocking fails with ENXIO when no one is reading.
    const int fd = open((homeDir() / ".steam" / "steam.pipe").c_str(), O_WRONLY | O_NONBLOCK);
    if (fd < 0) {
        return false;
    }
    close(fd);
    return true;
}

void openProtonInstall() {
    runDetached({"xdg-open", "steam://install/1493710"}, "Steam's Proton install page could not be opened.");
}


std::optional<std::string> wineInstallCommand() {
    std::ifstream release("/etc/os-release");
    std::string line;
    std::vector<std::string> ids;
    while (std::getline(release, line)) {
        if (line.rfind("ID=", 0) == 0 || line.rfind("ID_LIKE=", 0) == 0) {
            std::string value = line.substr(line.find('=') + 1);
            std::replace(value.begin(), value.end(), '"', ' ');
            std::istringstream words(value);
            std::string word;
            while (words >> word) {
                ids.push_back(word);
            }
        }
    }
    auto has = [&ids](std::initializer_list<const char*> names) {
        return std::any_of(ids.begin(), ids.end(), [&names](const std::string& id) {
            return std::any_of(names.begin(), names.end(), [&id](const char* name) { return id == name; });
        });
    };
    if (has({"debian", "ubuntu"})) {
        return "apt-get update && (apt-get install -y wine64 || apt-get install -y wine)";
    }
    if (has({"fedora", "rhel"})) {
        return "dnf install -y wine";
    }
    if (has({"arch"})) {
        return "pacman -S --noconfirm wine";
    }
    if (has({"suse", "opensuse", "opensuse-tumbleweed"})) {
        return "zypper --non-interactive install wine";
    }
    return std::nullopt;
}

std::string detectedSteamPath() {
    const auto root = detectedSteamRoot();
    return root ? root->string() : std::string();
}

std::string detectedWinePath() {
    const fs::path system = findProgram(envOr("GWLAUNCHER_WINE", "wine"));
    if (!system.empty()) {
        return system.string();
    }
    if (const auto root = detectedSteamRoot()) {
        if (const auto files = steamProtonFiles(*root)) {
            return (*files / "bin" / "wine").string();
        }
    }
    return {};
}

void setInstallPaths(const std::string& steamPath, const std::string& winePath) {
    manualSteamPath = steamPath;
    manualWinePath = winePath;
}

std::optional<std::string> invalidInstallPath(const std::string& steamPath, const std::string& winePath) {
    if (!steamPath.empty() && !steamInstallAt(steamPath)) {
        return "There's no Steam client in " + steamPath + ": its legacycompat folder is missing.";
    }
    if (!winePath.empty() && findProgram(winePath).empty()) {
        return "There's no runnable Wine program at " + winePath + ".";
    }
    return std::nullopt;
}

std::filesystem::path defaultGameFolder() {
    return launcherDataDir() / "Guild Wars";
}

void setDefaultGame(const std::filesystem::path& exe) {
    setGamePath(systemRuntime(launcherDataDir()), exe.string());
}

}  // namespace gwl
