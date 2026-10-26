// Windows: starting a Guild Wars client and waiting until its window is ready. The game starts suspended. The multiclient patch and
// the DLLs are applied while it's suspended, then it's resumed. A port of the Rust launcher's game, process, inject and multiclient
// modules, with the same order, timeouts and messages.

#include <windows.h>
#include <sddl.h>
#include <tlhelp32.h>
#include <winsafer.h>

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <functional>
#include <iterator>
#include <memory>
#include <mutex>
#include <optional>
#include <set>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "Progress.h"
#include "Accounts.h"
#include "CliGame.h"
#include "GameLaunch.h"
#include "Launch.h"
#include "ModFiles.h"
#include "Modlist.h"
#include "utils/Bytes.h"
#include "utils/Files.h"
#include "utils/Strings.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

const int kWaitStepMs = 200;
const DWORD kTitleTimeoutMs = 5000;
const DWORD kInputIdleMs = 1000;
const UINT kWmSetText = 0x0C;
const DWORD kProcessWow64Information = 26;
const char* const kMediumIntegritySid = "S-1-16-8192";

// The multiclient patch: the bytes the game's single-instance check starts with, and the patch that makes it report that no other copy
// is running (xor eax,eax; nop; ret). The patch starts 0x1A bytes before the signature, in the game's code section.
const std::uint8_t kMulticlientSignature[19] = {0x56, 0x57, 0x68, 0x00, 0x01, 0x00, 0x00, 0x89, 0x85, 0xF4,
                                                0xFE, 0xFF, 0xFF, 0xC7, 0x00, 0x00, 0x00, 0x00, 0x00};
const std::uint8_t kMulticlientPayload[4] = {0x31, 0xC0, 0x90, 0xC3};
const std::size_t kMulticlientSignatureOffset = 0x1A;
const std::size_t kMulticlientSearchStart = 0x1000;
const std::size_t kMulticlientSearchLength = 0x48D000;

// "<step> failed: <Windows' message for the last error>".
std::string lastError(const char* step) {
    const DWORD code = GetLastError();
    return std::string(step) + " failed: " + std::system_category().message(static_cast<int>(code));
}

// The Win32 functions the launch needs from ntdll, looked up once.
using NtQueryInformationProcessFn = LONG(__stdcall*)(HANDLE, ULONG, PVOID, ULONG, PULONG);

NtQueryInformationProcessFn ntQueryInformationProcess() {
    static const NtQueryInformationProcessFn function = reinterpret_cast<NtQueryInformationProcessFn>(
        GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtQueryInformationProcess"));
    if (!function) {
        throw std::runtime_error("ntdll isn't available.");
    }
    return function;
}

// Whether this runs under Wine, which exports wine_get_version from ntdll.
bool isWine() {
    const HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
    return ntdll && GetProcAddress(ntdll, "wine_get_version");
}

// A process started suspended. Its handles are closed when it goes out of scope.
class GameProcess {
public:
    GameProcess(DWORD processId, HANDLE thread, HANDLE process) : processId_(processId), thread_(thread), process_(process) {}
    ~GameProcess() {
        CloseHandle(thread_);
        CloseHandle(process_);
    }
    GameProcess(const GameProcess&) = delete;
    GameProcess& operator=(const GameProcess&) = delete;

    DWORD processId() const { return processId_; }
    HANDLE handle() const { return process_; }

    void resume() const {
        if (ResumeThread(thread_) == static_cast<DWORD>(-1)) {
            throw std::runtime_error(lastError("WinApi.ResumeThread"));
        }
    }

    // Ends the process through its own handle, for a launch that has failed.
    void terminate() const {
        TerminateProcess(process_, 1);
    }

private:
    DWORD processId_;
    HANDLE thread_;
    HANDLE process_;
};

// The medium integrity level, which a launch's process runs at unless the account is elevated.
void setMediumIntegrity(HANDLE token) {
    PSID sid = nullptr;
    if (!ConvertStringSidToSidW(toWide(kMediumIntegritySid).c_str(), &sid)) {
        throw std::runtime_error(lastError("ConvertStringSidToSidW"));
    }
    TOKEN_MANDATORY_LABEL label{};
    label.Label.Sid = sid;
    label.Label.Attributes = SE_GROUP_INTEGRITY;
    const DWORD length = static_cast<DWORD>(sizeof(TOKEN_MANDATORY_LABEL) + GetLengthSid(sid));
    const BOOL set = SetTokenInformation(token, TokenIntegrityLevel, &label, length);
    LocalFree(sid);
    if (!set) {
        throw std::runtime_error(lastError("SetTokenInformation"));
    }
}

// A token for a normal user at medium integrity, so the game doesn't run with the launcher's own rights.
HANDLE restrictedToken() {
    SAFER_LEVEL_HANDLE level = nullptr;
    if (!SaferCreateLevel(SAFER_SCOPEID_USER, SAFER_LEVELID_NORMALUSER, SAFER_LEVEL_OPEN, &level, nullptr)) {
        throw std::runtime_error(lastError("SaferCreateLevel"));
    }
    HANDLE token = nullptr;
    const BOOL computed = SaferComputeTokenFromLevel(level, nullptr, &token, 0, nullptr);
    const BOOL closed = SaferCloseLevel(level);
    if (!computed) {
        throw std::runtime_error(lastError("SaferComputeTokenFromLevel"));
    }
    if (!closed) {
        CloseHandle(token);
        throw std::runtime_error(lastError("SaferCloseLevel"));
    }
    try {
        setMediumIntegrity(token);
    } catch (...) {
        CloseHandle(token);
        throw;
    }
    return token;
}

// Starts the game suspended, in the game's own folder. Wine has no integrity levels, so it starts the game without a token.
std::unique_ptr<GameProcess> createSuspended(const std::string& gwpath, const std::string& commandLine, bool elevated) {
    const fs::path parent = pathFromUtf8(gwpath).parent_path();
    if (parent.empty()) {
        throw std::runtime_error("the game path has no folder");
    }
    const std::wstring folder = parent.wstring();
    std::wstring command = toWide(commandLine);
    STARTUPINFOW startup{};
    startup.cb = sizeof(STARTUPINFOW);
    PROCESS_INFORMATION info{};

    if (elevated || isWine()) {
        if (!CreateProcessW(nullptr, command.data(), nullptr, nullptr, FALSE, CREATE_SUSPENDED, nullptr, folder.c_str(), &startup, &info)) {
            throw std::runtime_error(lastError("CreateProcessW"));
        }
    } else {
        HANDLE token = restrictedToken();
        const BOOL created = CreateProcessAsUserW(token, nullptr, command.data(), nullptr, nullptr, FALSE, CREATE_SUSPENDED, nullptr,
                                                  folder.c_str(), &startup, &info);
        const DWORD error = GetLastError();
        CloseHandle(token);
        if (!created) {
            SetLastError(error);
            throw std::runtime_error(lastError("CreateProcessAsUserW"));
        }
    }
    return std::make_unique<GameProcess>(info.dwProcessId, info.hThread, info.hProcess);
}

// Reads a block of memory from a process, failing when it can't all be read.
std::vector<std::uint8_t> readMemory(HANDLE process, std::uintptr_t address, std::size_t length) {
    std::vector<std::uint8_t> bytes(length);
    SIZE_T read = 0;
    if (!ReadProcessMemory(process, reinterpret_cast<LPCVOID>(address), bytes.data(), length, &read) || read != length) {
        throw std::runtime_error("the read at " + std::to_string(address) + " was short");
    }
    return bytes;
}

std::uint32_t readU32At(HANDLE process, std::uintptr_t address) {
    return readU32LE(readMemory(process, address, 4), 0);
}

// The address of a 32-bit process's PEB. It's available while the process is still suspended.
std::uintptr_t peb32(HANDLE process) {
    std::uintptr_t peb = 0;
    ULONG returned = 0;
    const LONG status = ntQueryInformationProcess()(process, kProcessWow64Information, &peb, sizeof(peb), &returned);
    if (status != 0 || peb == 0) {
        throw std::runtime_error("NtQueryInformationProcess failed with " + std::to_string(status));
    }
    return peb;
}

// The base address of the game's executable, which the PEB holds at offset 8.
std::uintptr_t imageBase(HANDLE process) {
    const std::uintptr_t base = readU32At(process, peb32(process) + 8);
    if (base == 0) {
        throw std::runtime_error("the game's PEB has no image base yet");
    }
    return base;
}

// The base of kernel32.dll in a 32-bit process, read from its loader list. It works once the process has started.
std::uintptr_t kernel32Base(HANDLE process) {
    const std::uintptr_t peb = peb32(process);
    const std::uintptr_t head = readU32At(process, peb + 0x0C) + 0x0C;
    std::uintptr_t entry = readU32At(process, head);
    for (int i = 0; i < 512 && entry != head && entry != 0; ++i) {
        // LDR_DATA_TABLE_ENTRY (32-bit): DllBase at 0x18, and BaseDllName, a UNICODE_STRING, at 0x2C.
        const std::vector<std::uint8_t> fields = readMemory(process, entry, 0x34);
        const std::uintptr_t dllBase = readU32LE(fields, 0x18);
        const std::size_t nameLength = readU16LE(fields, 0x2C);
        const std::uintptr_t nameBuffer = readU32LE(fields, 0x30);
        const std::vector<std::uint8_t> nameBytes = readMemory(process, nameBuffer, nameLength);
        std::wstring name;
        for (std::size_t j = 0; j + 1 < nameBytes.size(); j += 2) {
            name.push_back(static_cast<wchar_t>(readU16LE(nameBytes, j)));
        }
        if (_wcsicmp(name.c_str(), L"kernel32.dll") == 0) {
            return dllBase;
        }
        entry = readU32LE(fields, 0);
    }
    throw std::runtime_error("kernel32.dll isn't in the process's module list");
}

// The offset of an export in a 32-bit module, read from its export table.
std::uint32_t exportOffset(HANDLE process, std::uintptr_t base, const std::string& name) {
    const std::vector<std::uint8_t> headers = readMemory(process, base, 0x200);
    const std::uintptr_t nt = base + readU32LE(headers, 0x3C);
    const std::vector<std::uint8_t> ntHeader = readMemory(process, nt, 0x88);
    // The optional header follows the 24-byte signature and file header, and its first data directory is the export table.
    const std::uint32_t exportRva = readU32LE(ntHeader, 24 + 96);
    if (exportRva == 0) {
        throw std::runtime_error("the module has no export table");
    }
    const std::vector<std::uint8_t> dir = readMemory(process, base + exportRva, 40);
    const std::uint32_t count = readU32LE(dir, 24);
    const std::uintptr_t functions = base + readU32LE(dir, 28);
    const std::uintptr_t names = base + readU32LE(dir, 32);
    const std::uintptr_t ordinals = base + readU32LE(dir, 36);
    for (std::uint32_t index = 0; index < count; ++index) {
        const std::uintptr_t nameRva = readU32At(process, names + index * 4);
        const std::vector<std::uint8_t> text = readMemory(process, base + nameRva, name.size() + 1);
        if (std::memcmp(text.data(), name.data(), name.size()) == 0 && text[name.size()] == 0) {
            const std::uint16_t ordinal = readU16LE(readMemory(process, ordinals + index * 2, 2), 0);
            return readU32At(process, functions + static_cast<std::uintptr_t>(ordinal) * 4);
        }
    }
    throw std::runtime_error(name + " isn't exported");
}

// The 32-bit LoadLibraryW address. A 32-bit helper process is started, since kernel32 sits at the same place in every 32-bit process,
// and its address is read from the helper's module list. It's found once.
std::optional<std::uintptr_t> findLoadLibraryW() {
    wchar_t root[MAX_PATH] = L"";
    if (GetEnvironmentVariableW(L"SystemRoot", root, MAX_PATH) == 0) {
        lstrcpyW(root, L"C:\\Windows");
    }
    const std::wstring cmd = std::wstring(root) + L"\\SysWOW64\\cmd.exe";
    std::wstring commandLine = L"\"" + cmd + L"\" /c \"ping -n 10 127.0.0.1 >nul\"";
    STARTUPINFOW startup{};
    startup.cb = sizeof(STARTUPINFOW);
    PROCESS_INFORMATION info{};
    if (!CreateProcessW(cmd.c_str(), commandLine.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &info)) {
        return std::nullopt;
    }
    CloseHandle(info.hThread);

    std::optional<std::uintptr_t> result;
    for (int attempt = 0; attempt < 50; ++attempt) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        try {
            const std::uintptr_t base = kernel32Base(info.hProcess);
            result = base + exportOffset(info.hProcess, base, "LoadLibraryW");
            break;
        } catch (const std::runtime_error&) {
            // The helper's module list fills in as it starts, so it's tried again.
        }
    }
    TerminateProcess(info.hProcess, 1);
    WaitForSingleObject(info.hProcess, 5000);
    CloseHandle(info.hProcess);
    return result;
}

std::optional<std::uintptr_t> loadLibraryW() {
    static std::once_flag once;
    static std::optional<std::uintptr_t> address;
    std::call_once(once, [] { address = findLoadLibraryW(); });
    return address;
}

// What loading a DLL into the game ended with. The names are the C# GWCAMemory.LoadModuleResult's.
enum class ModuleLoad {
    Successful,
    ModuleNonexistent,
    Kernel32NotFound,
    LoadLibraryNotFound,
    MemoryNotAllocated,
    PathNotWritten,
    RemoteThreadNotSpawned,
    RemoteThreadDidNotStart,
    RemoteThreadDidNotFinish,
    MemoryNotDeallocated,
};

const char* moduleLoadName(ModuleLoad result) {
    switch (result) {
        case ModuleLoad::Successful:
            return "Successful";
        case ModuleLoad::ModuleNonexistent:
            return "ModuleNonexistent";
        case ModuleLoad::Kernel32NotFound:
            return "Kernel32NotFound";
        case ModuleLoad::LoadLibraryNotFound:
            return "LoadLibraryNotFound";
        case ModuleLoad::MemoryNotAllocated:
            return "MemoryNotAllocated";
        case ModuleLoad::PathNotWritten:
            return "PathNotWritten";
        case ModuleLoad::RemoteThreadNotSpawned:
            return "RemoteThreadNotSpawned";
        case ModuleLoad::RemoteThreadDidNotStart:
            return "RemoteThreadDidNotStart";
        case ModuleLoad::RemoteThreadDidNotFinish:
            return "RemoteThreadDidNotFinish";
        case ModuleLoad::MemoryNotDeallocated:
            return "MemoryNotDeallocated";
    }
    return "Unknown";
}

// Loads a DLL into the game with LoadLibraryW on a remote thread, the same steps and results as the C# GWCAMemory.LoadModule.
ModuleLoad loadModule(HANDLE process, const fs::path& module, DWORD timeoutMs) {
    std::error_code error;
    const fs::path full = fs::absolute(module, error);
    if (error || !fs::is_regular_file(full, error)) {
        return ModuleLoad::ModuleNonexistent;
    }
    const std::wstring pathWide = full.wstring();
    const std::size_t byteCount = (pathWide.size() + 1) * sizeof(wchar_t);

    const std::optional<std::uintptr_t> loadLibrary = loadLibraryW();
    if (!loadLibrary) {
        return ModuleLoad::LoadLibraryNotFound;
    }

    void* buffer = VirtualAllocEx(process, nullptr, byteCount, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!buffer) {
        return ModuleLoad::MemoryNotAllocated;
    }
    const auto release = [process, buffer] { return VirtualFreeEx(process, buffer, 0, MEM_RELEASE) != FALSE; };

    std::vector<std::uint8_t> bytes(byteCount, 0);
    std::memcpy(bytes.data(), pathWide.c_str(), pathWide.size() * sizeof(wchar_t));
    SIZE_T written = 0;
    bool pathWritten = WriteProcessMemory(process, buffer, bytes.data(), byteCount, &written) && written == byteCount;
    if (pathWritten) {
        // The path is read back in full, so a path longer than the C# check's 260 bytes is checked too.
        try {
            pathWritten = readMemory(process, reinterpret_cast<std::uintptr_t>(buffer), byteCount) == bytes;
        } catch (const std::runtime_error&) {
            pathWritten = false;
        }
    }
    if (!pathWritten) {
        release();
        return ModuleLoad::PathNotWritten;
    }

    DWORD threadId = 0;
    HANDLE thread = CreateRemoteThread(process, nullptr, 0, reinterpret_cast<LPTHREAD_START_ROUTINE>(*loadLibrary),
                                       buffer, 0, &threadId);
    if (!thread) {
        release();
        return ModuleLoad::RemoteThreadNotSpawned;
    }

    const DWORD waited = WaitForSingleObject(thread, timeoutMs);
    ModuleLoad result = ModuleLoad::Successful;
    if (waited != WAIT_OBJECT_0) {
        result = ModuleLoad::RemoteThreadDidNotStart;
    } else {
        DWORD exitCode = 0;
        if (!GetExitCodeThread(thread, &exitCode)) {
            result = ModuleLoad::RemoteThreadDidNotFinish;
        }
    }
    CloseHandle(thread);

    if (result != ModuleLoad::Successful) {
        release();
        return result;
    }
    return release() ? ModuleLoad::Successful : ModuleLoad::MemoryNotDeallocated;
}

// Patches the game's single-instance check, so several copies can run. Returns the address patched.
std::uintptr_t applyMulticlient(HANDLE process) {
    std::uintptr_t base = 0;
    try {
        base = imageBase(process);
    } catch (const std::runtime_error& error) {
        throw std::runtime_error(std::string("reading the game's image base failed: ") + error.what());
    }
    const std::uintptr_t searchStart = base + kMulticlientSearchStart;

    std::vector<std::uint8_t> image(kMulticlientSearchLength);
    SIZE_T read = 0;
    if (!ReadProcessMemory(process, reinterpret_cast<LPCVOID>(searchStart), image.data(), image.size(), &read)) {
        throw std::runtime_error(lastError("ReadProcessMemory"));
    }
    image.resize(read);

    const auto found = std::search(image.begin(), image.end(), std::begin(kMulticlientSignature), std::end(kMulticlientSignature));
    if (found == image.end()) {
        throw std::runtime_error("the single-instance check wasn't found in the game");
    }
    const auto index = static_cast<std::size_t>(found - image.begin());
    if (index < kMulticlientSignatureOffset) {
        throw std::runtime_error("the signature is too close to the image start");
    }
    const std::uintptr_t target = searchStart + index - kMulticlientSignatureOffset;

    SIZE_T written = 0;
    if (!WriteProcessMemory(process, reinterpret_cast<LPVOID>(target), kMulticlientPayload, sizeof(kMulticlientPayload), &written)) {
        throw std::runtime_error(lastError("WriteProcessMemory"));
    }
    return target;
}

// The windows of the launcher's game search: the first visible top-level window of the process that has no owner.
struct WindowSearch {
    DWORD processId;
    HWND found;
};

BOOL CALLBACK visitWindow(HWND window, LPARAM lparam) {
    auto* search = reinterpret_cast<WindowSearch*>(lparam);
    DWORD owner = 0;
    GetWindowThreadProcessId(window, &owner);
    const bool owned = GetWindow(window, GW_OWNER) != nullptr;
    if (owner == search->processId && IsWindowVisible(window) && !owned) {
        search->found = window;
        return FALSE;
    }
    return TRUE;
}

HWND mainWindow(DWORD processId) {
    WindowSearch search{processId, nullptr};
    EnumWindows(visitWindow, reinterpret_cast<LPARAM>(&search));
    return search.found;
}

std::wstring windowTitle(HWND window) {
    if (!window) {
        return L"";
    }
    wchar_t buffer[512];
    const int length = GetWindowTextW(window, buffer, 512);
    return std::wstring(buffer, static_cast<std::size_t>(std::max(length, 0)));
}

bool isGameTitle(const std::wstring& title) {
    return title == L"Guild Wars" || title == L"Guild Wars Reforged";
}

bool isResponding(HWND window) {
    return !window || !IsHungAppWindow(window);
}

// Count time spent in the check too, since WaitForInputIdle can block.
bool waitFor(const std::function<bool()>& test, DWORD timeoutMs) {
    const auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeoutMs);
    bool ok = test();
    while (!ok && std::chrono::steady_clock::now() < deadline) {
        std::this_thread::sleep_for(std::chrono::milliseconds(kWaitStepMs));
        ok = test();
    }
    return ok;
}

// Waits for the window, its input and its title, then checks the game still responds. The game is ended if a wait fails.
void waitForWindow(const GameProcess& game, const std::string& name, DWORD timeoutMs) {
    const DWORD processId = game.processId();
    const auto fail = [&game](const std::string& message) {
        game.terminate();
        throw std::runtime_error(message);
    };

    const std::string timeoutSeconds = std::to_string(timeoutMs / 1000);
    if (!waitFor([processId] { return mainWindow(processId) != nullptr; }, timeoutMs)) {
        fail("Failed to wait for MainWindowHandle after " + timeoutSeconds + " seconds. Increase Launch timeout in Settings > Advanced.");
    }
    if (!waitFor([&game] { return WaitForInputIdle(game.handle(), kInputIdleMs) == 0; }, timeoutMs)) {
        fail("Failed to wait for WaitForInputIdle after " + timeoutSeconds + " seconds. Increase Launch timeout in Settings > Advanced.");
    }
    SetForegroundWindow(mainWindow(processId));

    // The character-name wait needs the game's memory reader, which isn't ported yet, so the launch only waits for the title.
    const bool titleReady = waitFor([processId] { return !windowTitle(mainWindow(processId)).empty(); }, kTitleTimeoutMs);
    if (titleReady && isGameTitle(windowTitle(mainWindow(processId)))) {
        waitFor(
            [processId, &name] {
                const HWND window = mainWindow(processId);
                if (window) {
                    SendMessageA(window, kWmSetText, 0, reinterpret_cast<LPARAM>(name.c_str()));
                }
                return !isGameTitle(windowTitle(mainWindow(processId)));
            },
            kTitleTimeoutMs);
    }

    if (!isResponding(mainWindow(processId))) {
        fail("Failed to wait for process to respond after " + std::to_string(kTitleTimeoutMs / 1000) + " seconds.");
    }
}

// Every running process as (id, file name without .exe).
struct ProcessEntry {
    DWORD processId;
    std::wstring stem;
};

std::vector<ProcessEntry> processEntries() {
    std::vector<ProcessEntry> entries;
    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) {
        return entries;
    }
    PROCESSENTRY32W entry{};
    entry.dwSize = sizeof(PROCESSENTRY32W);
    if (Process32FirstW(snapshot, &entry)) {
        do {
            entries.push_back({entry.th32ProcessID, fs::path(entry.szExeFile).stem().wstring()});
        } while (Process32NextW(snapshot, &entry));
    }
    CloseHandle(snapshot);
    return entries;
}

// The image path a process runs from, or nothing when it can't be read.
std::optional<std::string> imagePath(DWORD processId) {
    HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
    if (!process) {
        return std::nullopt;
    }
    std::wstring buffer(1024, L'\0');
    DWORD length = static_cast<DWORD>(buffer.size());
    const bool read = QueryFullProcessImageNameW(process, 0, buffer.data(), &length) != FALSE;
    CloseHandle(process);
    if (!read) {
        return std::nullopt;
    }
    return toUtf8(buffer.substr(0, length));
}

bool processExists(const std::wstring& stem) {
    const auto entries = processEntries();
    return std::any_of(entries.begin(), entries.end(), [&stem](const ProcessEntry& entry) { return entry.stem == stem; });
}

// A running process whose image path contains this game path, matched on the file name first, as the C# IsProcessOpen does.
bool isRunningFrom(const std::string& gwpath) {
    const std::wstring stem = pathFromUtf8(gwpath).stem().wstring();
    for (const ProcessEntry& entry : processEntries()) {
        if (entry.stem != stem) {
            continue;
        }
        const auto path = imagePath(entry.processId);
        if (path && path->find(gwpath) != std::string::npos) {
            return true;
        }
    }
    return false;
}

// Removes the Steam app-id file when the launch ends, unless the game started, which Steam needs to read it after its window is up.
class SteamAppIdFile {
public:
    explicit SteamAppIdFile(fs::path path) : path_(std::move(path)) {}
    ~SteamAppIdFile() {
        if (!keep_) {
            std::error_code error;
            fs::remove(path_, error);
        }
    }
    void keep() { keep_ = true; }

private:
    fs::path path_;
    bool keep_ = false;
};

// Starts the game suspended, patches it, loads its mods, then resumes it. The game is ended on any failure.
std::unique_ptr<GameProcess> startPatched(const Account& account, const fs::path& launcherDir, std::uint32_t timeoutMs,
                                          const std::function<bool()>& confirm) {
    const fs::path gwPath = pathFromUtf8(account.gwpath);
    const fs::path currentDir = fs::current_path();
    const auto gmod = findGmod(launcherDir, currentDir);
    const ModSelection selection = accountSelection(account, launcherDir, gmod);
    std::optional<fs::path> gmodDll;
    for (const fs::path& dll : selection.dlls) {
        if (isGmod(dll)) {
            gmodDll = dll;
        }
    }
    // Kept until this function returns, so gMod's list is put back before the window is waited for.
    const auto modlist = ModlistGuard::prepare(selection.texmods, gmodDll, gwPath);

    auto game = createSuspended(account.gwpath, gameCommandLine(account.gwpath, account), account.elevated);
    try {
        applyMulticlient(game->handle());
        if (confirm && !confirm()) {
            throw std::runtime_error(kCancelledMessage);
        }
        for (const fs::path& dll : selection.dlls) {
            const ModuleLoad result = loadModule(game->handle(), dll, timeoutMs);
            if (result == ModuleLoad::Successful) {
                continue;
            }
            if (result == ModuleLoad::RemoteThreadDidNotStart) {
                throw std::runtime_error("The mod " + pathToUtf8(dll.filename()) + " didn't load within the " + std::to_string(timeoutMs) +
                                         " ms limit. Increase Launch timeout under Settings > Advanced.");
            }
            throw std::runtime_error(std::string("Result ") + moduleLoadName(result) + ": memory.LoadModule(" + pathToUtf8(dll) + ")");
        }
        game->resume();
    } catch (const std::runtime_error&) {
        game->terminate();
        throw;
    }
    return game;
}

}  // namespace

bool steamRunning() {
    return processExists(L"steam");
}

std::set<std::string> runningGamePaths() {
    std::set<std::string> paths;
    for (const ProcessEntry& entry : processEntries()) {
        if (const auto path = imagePath(entry.processId)) {
            paths.insert(lowerCase(*path));
        }
    }
    return paths;
}

void launchGameInProcess(const Account& account, const std::string& dir, std::uint32_t timeoutMs, const std::function<bool()>& confirm,
                         bool requireSteam) {
    const auto appIdPath = steamAppIdPath(account.gwpath);
    if (!appIdPath) {
        throw std::runtime_error("Failed to determine Guild Wars directory path.");
    }
    std::error_code error;
    fs::remove(*appIdPath, error);
    if (account.isSteamAccount) {
        try {
            writeSteamAppId(account);
        } catch (const std::runtime_error& failure) {
            throw std::runtime_error(std::string("Failed to create steam_appid.txt: ") + failure.what());
        }
    }
    SteamAppIdFile appId(*appIdPath);

    if (!fs::is_regular_file(pathFromUtf8(account.gwpath), error)) {
        throw std::runtime_error("Invalid path to Gw.exe.");
    }
    if (requireSteam && account.isSteamAccount && !steamRunning()) {
        throw std::runtime_error("Start Steam before launching a Steam account.");
    }
    if (isRunningFrom(account.gwpath)) {
        throw std::runtime_error("The Guild Wars executable at " + account.gwpath + " is already running");
    }

    const auto game = startPatched(account, pathFromUtf8(dir), timeoutMs, confirm);
    waitForWindow(*game, account.name(), timeoutMs);
    appId.keep();
}

}  // namespace gwl
