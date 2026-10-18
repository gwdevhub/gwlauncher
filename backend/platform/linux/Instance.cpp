// Linux: requests go over a Unix socket in the launcher's data folder. A socket left by a launcher that exited is replaced.

#include "Instance.h"

#include <sys/socket.h>
#include <sys/time.h>
#include <sys/types.h>
#include <sys/un.h>
#include <unistd.h>

#include <cerrno>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <string>
#include <thread>

#include "Launch.h"

namespace gwl {

namespace {

std::string socketPath() {
    return (launcherDataDir() / "gwlauncher.sock").string();
}

// The address for the socket path, or false when the path is too long for one.
bool addressFor(const std::string& path, sockaddr_un& address) {
    if (path.size() >= sizeof(address.sun_path)) {
        return false;
    }
    address = sockaddr_un{};
    address.sun_family = AF_UNIX;
    std::memcpy(address.sun_path, path.c_str(), path.size() + 1);
    return true;
}

}  // namespace

bool forwardRequest(const InstanceRequest& request) {
    sockaddr_un address{};
    if (!addressFor(socketPath(), address)) {
        return false;
    }
    const int fd = socket(AF_UNIX, SOCK_STREAM | SOCK_CLOEXEC, 0);
    if (fd < 0) {
        return false;
    }
    bool sent = connect(fd, reinterpret_cast<sockaddr*>(&address), sizeof(address)) == 0;
    if (sent) {
        const std::string line = request.encode();
        sent = send(fd, line.data(), line.size(), MSG_NOSIGNAL) == static_cast<ssize_t>(line.size());
    }
    if (sent) {
        // The running launcher answers "ok" once it has taken the request. Without that, nothing is listening for it, and this
        // launcher starts instead.
        timeval wait{2, 0};
        setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, &wait, sizeof(wait));
        char reply[8] = {};
        const ssize_t received = recv(fd, reply, sizeof(reply) - 1, 0);
        sent = received >= 2 && std::string(reply, static_cast<std::size_t>(received)).rfind("ok", 0) == 0;
    }
    close(fd);
    return sent;
}

void listenForRequests(std::function<void(InstanceRequest)> handle) {
    const std::string path = socketPath();
    sockaddr_un address{};
    if (!addressFor(path, address)) {
        std::fprintf(stderr, "could not listen for other starts: the path is too long\n");
        return;
    }
    std::error_code ignored;
    std::filesystem::create_directories(launcherDataDir(), ignored);
    const int fd = socket(AF_UNIX, SOCK_STREAM | SOCK_CLOEXEC, 0);
    if (fd < 0) {
        return;
    }
    // Nothing answers on a socket left by a launcher that exited, since forwardRequest found none, so it's replaced.
    unlink(path.c_str());
    if (bind(fd, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0 || listen(fd, 8) != 0) {
        std::fprintf(stderr, "could not listen for other starts\n");
        close(fd);
        return;
    }
    std::thread([fd, handle = std::move(handle)] {
        for (;;) {
            const int client = accept4(fd, nullptr, nullptr, SOCK_CLOEXEC);
            if (client < 0) {
                if (errno == EINTR) {
                    continue;
                }
                break;
            }
            // A client that connects and says nothing gives up after two seconds, so it can't hold up the next one.
            timeval wait{2, 0};
            setsockopt(client, SOL_SOCKET, SO_RCVTIMEO, &wait, sizeof(wait));
            std::string line;
            char buffer[512];
            ssize_t received = 0;
            while (line.find('\n') == std::string::npos && (received = recv(client, buffer, sizeof(buffer), 0)) > 0) {
                line.append(buffer, static_cast<std::size_t>(received));
            }
            const auto request = InstanceRequest::decode(line);
            if (request) {
                send(client, "ok\n", 3, MSG_NOSIGNAL);
            }
            close(client);
            if (request) {
                handle(*request);
            }
        }
    }).detach();
}

}  // namespace gwl
