#include "Network.h"

#include <fcntl.h>
#include <netdb.h>
#include <netinet/in.h>
#include <netinet/tcp.h>
#include <sys/select.h>
#include <sys/socket.h>
#include <sys/time.h>
#include <unistd.h>

#include <cerrno>
#include <cstring>
#include <stdexcept>
#include <utility>

namespace gwl {

namespace {

const int kNoSocket = -1;

[[noreturn]] void connectionError() {
    throw std::runtime_error(std::string("The connection failed: ") + std::strerror(errno));
}

// Connects to one address, giving up after the connect timeout. Returns kNoSocket when it doesn't answer.
int connectAddress(const addrinfo* address, int connectTimeoutSeconds) {
    const int socket = ::socket(address->ai_family, address->ai_socktype, address->ai_protocol);
    if (socket == kNoSocket) {
        return kNoSocket;
    }
    fcntl(socket, F_SETFL, fcntl(socket, F_GETFL, 0) | O_NONBLOCK);
    bool connected = ::connect(socket, address->ai_addr, static_cast<socklen_t>(address->ai_addrlen)) == 0;
    if (!connected) {
        fd_set writable;
        FD_ZERO(&writable);
        FD_SET(socket, &writable);
        timeval timeout{connectTimeoutSeconds, 0};
        if (select(socket + 1, nullptr, &writable, nullptr, &timeout) == 1) {
            int error = 0;
            socklen_t size = sizeof(error);
            connected = getsockopt(socket, SOL_SOCKET, SO_ERROR, &error, &size) == 0 && error == 0;
        }
    }
    if (!connected) {
        close(socket);
        return kNoSocket;
    }
    fcntl(socket, F_SETFL, fcntl(socket, F_GETFL, 0) & ~O_NONBLOCK);
    return socket;
}

}  // namespace

struct TcpConnection::Impl {
    int socket = kNoSocket;

    ~Impl() {
        if (socket != kNoSocket) {
            close(socket);
        }
    }
};

TcpConnection::TcpConnection(std::unique_ptr<Impl> impl) : impl_(std::move(impl)) {}

TcpConnection::TcpConnection(TcpConnection&& other) noexcept = default;

TcpConnection& TcpConnection::operator=(TcpConnection&& other) noexcept = default;

TcpConnection::~TcpConnection() = default;

std::optional<TcpConnection> TcpConnection::connect(const std::string& host, const std::string& port, int connectTimeoutSeconds,
                                                    int receiveTimeoutSeconds) {
    addrinfo hints{};
    hints.ai_family = AF_UNSPEC;
    hints.ai_socktype = SOCK_STREAM;
    addrinfo* addresses = nullptr;
    if (getaddrinfo(host.c_str(), port.c_str(), &hints, &addresses) != 0) {
        return std::nullopt;
    }
    std::unique_ptr<addrinfo, decltype(&freeaddrinfo)> owned(addresses, freeaddrinfo);

    for (const addrinfo* address = addresses; address; address = address->ai_next) {
        const int socket = connectAddress(address, connectTimeoutSeconds);
        if (socket == kNoSocket) {
            continue;
        }
        auto impl = std::make_unique<Impl>();
        impl->socket = socket;
        timeval receiveTimeout{receiveTimeoutSeconds, 0};
        setsockopt(socket, SOL_SOCKET, SO_RCVTIMEO, &receiveTimeout, sizeof(receiveTimeout));
        int noDelay = 1;
        setsockopt(socket, IPPROTO_TCP, TCP_NODELAY, &noDelay, sizeof(noDelay));
        return TcpConnection(std::move(impl));
    }
    return std::nullopt;
}

void TcpConnection::send(const std::vector<std::uint8_t>& bytes) {
    std::size_t sent = 0;
    while (sent < bytes.size()) {
        const ssize_t count = ::send(impl_->socket, bytes.data() + sent, bytes.size() - sent, MSG_NOSIGNAL);
        if (count <= 0) {
            connectionError();
        }
        sent += static_cast<std::size_t>(count);
    }
}

std::vector<std::uint8_t> TcpConnection::receive(std::size_t count) {
    std::vector<std::uint8_t> bytes(count);
    std::size_t got = 0;
    while (got < count) {
        const ssize_t read = ::recv(impl_->socket, bytes.data() + got, count - got, 0);
        if (read <= 0) {
            connectionError();
        }
        got += static_cast<std::size_t>(read);
    }
    return bytes;
}

}  // namespace gwl
