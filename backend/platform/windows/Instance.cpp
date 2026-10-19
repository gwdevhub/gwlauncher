// Windows: requests go over a named pipe, one per user, so each user's launcher takes the requests of that user's starts.

#include "Instance.h"

#include <windows.h>

#include <chrono>
#include <cstddef>
#include <string>
#include <thread>

namespace gwl {

namespace {

std::wstring pipeName() {
    wchar_t user[256] = {};
    DWORD size = sizeof(user) / sizeof(user[0]);
    std::wstring name = L"\\\\.\\pipe\\GwLauncher-";
    if (GetUserNameW(user, &size)) {
        name += user;
    }
    return name;
}

// One line, up to its newline. False when the pipe closes or fails first.
bool readLine(HANDLE pipe, std::string& line) {
    char buffer[512];
    while (line.find('\n') == std::string::npos) {
        DWORD read = 0;
        if (!ReadFile(pipe, buffer, sizeof(buffer), &read, nullptr) || read == 0) {
            return false;
        }
        line.append(buffer, read);
    }
    return true;
}

}  // namespace

bool forwardRequest(const InstanceRequest& request) {
    const std::wstring name = pipeName();
    // No pipe means no launcher, and a busy one is waited for.
    if (!WaitNamedPipeW(name.c_str(), 2000)) {
        return false;
    }
    HANDLE pipe = CreateFileW(name.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
    if (pipe == INVALID_HANDLE_VALUE) {
        return false;
    }
    // The running launcher answers "ok" once it has taken the request. Without that, nothing is listening for it, and this launcher
    // starts instead.
    const std::string line = request.encode();
    DWORD written = 0;
    bool sent = WriteFile(pipe, line.data(), static_cast<DWORD>(line.size()), &written, nullptr) && written == line.size();
    std::string reply;
    sent = sent && readLine(pipe, reply) && reply.rfind("ok", 0) == 0;
    CloseHandle(pipe);
    return sent;
}

void listenForRequests(std::function<void(InstanceRequest)> handle) {
    const std::wstring name = pipeName();
    std::thread([name, handle = std::move(handle)] {
        bool first = true;
        for (;;) {
            // Only the first pipe can be made by this launcher; a launcher that finds it taken is listened to by another one.
            const DWORD openMode = PIPE_ACCESS_DUPLEX | (first ? FILE_FLAG_FIRST_PIPE_INSTANCE : 0);
            HANDLE pipe = CreateNamedPipeW(name.c_str(), openMode, PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT, PIPE_UNLIMITED_INSTANCES,
                                           512, 512, 2000, nullptr);
            if (pipe == INVALID_HANDLE_VALUE) {
                if (first) {
                    return;
                }
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
                continue;
            }
            first = false;
            if (!ConnectNamedPipe(pipe, nullptr) && GetLastError() != ERROR_PIPE_CONNECTED) {
                CloseHandle(pipe);
                continue;
            }
            std::string line;
            const auto request = readLine(pipe, line) ? InstanceRequest::decode(line) : std::nullopt;
            if (request) {
                DWORD written = 0;
                WriteFile(pipe, "ok\n", 3, &written, nullptr);
                FlushFileBuffers(pipe);
            }
            DisconnectNamedPipe(pipe);
            CloseHandle(pipe);
            if (request) {
                handle(*request);
            }
        }
    }).detach();
}

}  // namespace gwl
