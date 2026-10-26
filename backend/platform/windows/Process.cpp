// Windows: running programs with CreateProcess, with no console window.

#include "Process.h"
#include "utils/Strings.h"

#include <windows.h>

namespace gwl {

namespace {

// The command line for argv, with each argument quoted so spaces survive.
std::wstring commandLine(const std::vector<std::string>& argv) {
    std::string line;
    for (const auto& arg : argv) {
        if (!line.empty()) {
            line += ' ';
        }
        line += '"';
        for (char c : arg) {
            if (c == '"') {
                line += '\\';
            }
            line += c;
        }
        line += '"';
    }
    return wide(line);
}

bool start(const std::vector<std::string>& argv, PROCESS_INFORMATION& info) {
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    std::wstring line = commandLine(argv);
    return CreateProcessW(nullptr, line.data(), nullptr, nullptr, FALSE, CREATE_NO_WINDOW, nullptr, nullptr, &startup, &info) != 0;
}

}  // namespace

int runProgram(const std::vector<std::string>& argv) {
    PROCESS_INFORMATION info{};
    if (!start(argv, info)) {
        return -1;
    }
    WaitForSingleObject(info.hProcess, INFINITE);
    DWORD code = 1;
    GetExitCodeProcess(info.hProcess, &code);
    CloseHandle(info.hThread);
    CloseHandle(info.hProcess);
    return static_cast<int>(code);
}

void runProgramDetached(const std::vector<std::string>& argv) {
    PROCESS_INFORMATION info{};
    if (start(argv, info)) {
        CloseHandle(info.hThread);
        CloseHandle(info.hProcess);
    }
}

}  // namespace gwl
