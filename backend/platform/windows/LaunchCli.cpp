// Windows: the launcher restarts itself with --launch to start a game without opening its UI.

#include <windows.h>

#include <atomic>
#include <filesystem>
#include <fstream>
#include <functional>
#include <sstream>
#include <stdexcept>
#include <string>

#include "Progress.h"
#include "Accounts.h"
#include "Launch.h"
#include "Shell.h"
#include "utils/Strings.h"
#include "utils/Files.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

std::string readText(const fs::path& path) {
    std::ifstream file(path, std::ios::binary);
    std::stringstream buffer;
    buffer << file.rdbuf();
    return trimmed(buffer.str());
}

// A handle that's closed when it goes out of scope.
class Handle {
public:
    explicit Handle(HANDLE handle = nullptr) : handle_(handle) {}
    ~Handle() {
        if (handle_ && handle_ != INVALID_HANDLE_VALUE) {
            CloseHandle(handle_);
        }
    }
    Handle(Handle&& other) noexcept : handle_(other.handle_) { other.handle_ = nullptr; }
    Handle& operator=(Handle&& other) noexcept {
        reset();
        handle_ = other.handle_;
        other.handle_ = nullptr;
        return *this;
    }
    Handle(const Handle&) = delete;
    Handle& operator=(const Handle&) = delete;
    HANDLE get() const { return handle_; }
    void reset() {
        if (handle_ && handle_ != INVALID_HANDLE_VALUE) {
            CloseHandle(handle_);
        }
        handle_ = nullptr;
    }

private:
    HANDLE handle_;
};

// Reads one line from a pipe, without its line break. Nothing when the pipe ends first.
bool readLine(HANDLE pipe, std::string& line) {
    line.clear();
    for (;;) {
        char c = 0;
        DWORD read = 0;
        if (!ReadFile(pipe, &c, 1, &read, nullptr) || read == 0) {
            return !line.empty();
        }
        if (c == '\n') {
            return true;
        }
        if (c != '\r') {
            line.push_back(c);
        }
    }
}

}  // namespace

void startGame(const Account& account, const std::string& dir, std::uint32_t timeoutMs, const std::function<bool()>& confirm,
               bool requireSteam) {
    (void)dir;
    if (requireSteam && account.isSteamAccount && !steamRunning()) {
        throw std::runtime_error("Steam is not running. Please start Steam before launching a Steam account.");
    }
    const fs::path cli = executablePath();
    const fs::path folder = cli.parent_path();

    // The account goes in a file beside the command-line launcher, in the form Accounts.json holds it.
    static std::atomic<unsigned> counter{0};
    const std::string name = "launch-account-" + std::to_string(GetCurrentProcessId()) + "-" + std::to_string(counter++) + ".json";
    const fs::path accountFile = folder / name;
    const fs::path errorFile = folder / (name + ".errors");
    {
        std::ofstream file(accountFile, std::ios::binary | std::ios::trunc);
        file << account.fileJson().dump();
        if (!file) {
            throw std::runtime_error("The launch file couldn't be written to " + pathToUtf8(accountFile));
        }
    }
    struct Cleanup {
        fs::path accountFile;
        fs::path errorFile;
        ~Cleanup() {
            std::error_code error;
            fs::remove(accountFile, error);
            fs::remove(errorFile, error);
        }
    } cleanup{accountFile, errorFile};

    SECURITY_ATTRIBUTES inheritable{sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE};
    // The game's output goes to a file, not the pipes, so the game can't keep the pipes open after the launcher is done with them.
    Handle errors(CreateFileW(errorFile.c_str(), GENERIC_WRITE, FILE_SHARE_READ, &inheritable, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr));
    if (errors.get() == INVALID_HANDLE_VALUE) {
        throw std::runtime_error("The launch's error file couldn't be made.");
    }

    Handle childInput;
    Handle childOutput;
    Handle ourInput;
    Handle ourOutput;
    if (confirm) {
        HANDLE readEnd = nullptr;
        HANDLE writeEnd = nullptr;
        if (!CreatePipe(&readEnd, &writeEnd, &inheritable, 0)) {
            throw std::runtime_error("A pipe couldn't be made.");
        }
        childInput = Handle(readEnd);
        ourInput = Handle(writeEnd);
        SetHandleInformation(ourInput.get(), HANDLE_FLAG_INHERIT, 0);
        if (!CreatePipe(&readEnd, &writeEnd, &inheritable, 0)) {
            throw std::runtime_error("A pipe couldn't be made.");
        }
        ourOutput = Handle(readEnd);
        childOutput = Handle(writeEnd);
        SetHandleInformation(ourOutput.get(), HANDLE_FLAG_INHERIT, 0);
    } else {
        childInput = Handle(CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &inheritable, OPEN_EXISTING, 0, nullptr));
        childOutput = Handle(CreateFileW(L"NUL", GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, &inheritable, OPEN_EXISTING, 0, nullptr));
    }

    std::wstring command = L"\"" + cli.wstring() + L"\" --launch \"" + accountFile.wstring() + L"\" " + std::to_wstring(timeoutMs);
    if (confirm) {
        command += L" --confirm";
    }
    STARTUPINFOW startup{};
    startup.cb = sizeof(STARTUPINFOW);
    startup.dwFlags = STARTF_USESTDHANDLES;
    startup.hStdInput = childInput.get();
    startup.hStdOutput = childOutput.get();
    startup.hStdError = errors.get();
    PROCESS_INFORMATION info{};
    const std::wstring folderWide = folder.wstring();
    if (!CreateProcessW(nullptr, command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, folderWide.c_str(), &startup, &info)) {
        throw std::runtime_error("The command-line launcher couldn't be started: " + std::to_string(GetLastError()));
    }
    Handle process(info.hProcess);
    Handle thread(info.hThread);
    // The child's ends are closed here, so the pipes end when the child does.
    childInput.reset();
    childOutput.reset();

    const auto failure = [&errorFile] {
        const std::string reason = readText(errorFile);
        return std::runtime_error(reason.empty() ? std::string("The game didn't start.") : reason);
    };
    const auto exitCode = [&process]() {
        WaitForSingleObject(process.get(), INFINITE);
        DWORD code = 1;
        GetExitCodeProcess(process.get(), &code);
        return code;
    };

    if (!confirm) {
        if (exitCode() != 0) {
            throw failure();
        }
        return;
    }

    // The command-line launcher prints "suspended" once the game waits, and the answer goes back on its stdin.
    std::string first;
    if (!readLine(ourOutput.get(), first) || first != "suspended") {
        ourInput.reset();
        exitCode();
        throw failure();
    }
    const bool resume = confirm();
    const std::string answer = resume ? "resume\n" : "cancel\n";
    DWORD written = 0;
    WriteFile(ourInput.get(), answer.data(), static_cast<DWORD>(answer.size()), &written, nullptr);
    ourInput.reset();
    if (!resume) {
        exitCode();
        throw std::runtime_error(kCancelledMessage);
    }
    if (exitCode() != 0) {
        throw failure();
    }
}

}  // namespace gwl
