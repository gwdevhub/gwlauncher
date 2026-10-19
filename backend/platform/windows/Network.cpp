#include "Network.h"

#include <winsock2.h>
#include <ws2tcpip.h>

#include <stdexcept>
#include <string>
#include <utility>

namespace gwl {

namespace {

const SOCKET kNoSocket = INVALID_SOCKET;

// Winsock starts once, before its first socket is made.
void startWinsock() {
    static const bool started = [] {
        WSADATA data{};
        return WSAStartup(MAKEWORD(2, 2), &data) == 0;
    }();
    if (!started) {
        throw std::runtime_error("The network couldn't be started.");
    }
}

[[noreturn]] void connectionError() {
    throw std::runtime_error("The connection failed: " + std::to_string(WSAGetLastError()));
}

// Connects to one address, giving up after the connect timeout. Returns kNoSocket when it doesn't answer.
SOCKET connectAddress(const addrinfo* address, int connectTimeoutSeconds) {
    const SOCKET socket = ::socket(address->ai_family, address->ai_socktype, address->ai_protocol);
    if (socket == kNoSocket) {
        return kNoSocket;
    }
    u_long nonBlocking = 1;
    ioctlsocket(socket, FIONBIO, &nonBlocking);
    bool connected = ::connect(socket, address->ai_addr, static_cast<int>(address->ai_addrlen)) == 0;
    if (!connected) {
        fd_set writable;
        FD_ZERO(&writable);
        FD_SET(socket, &writable);
        timeval timeout{connectTimeoutSeconds, 0};
        if (select(0, nullptr, &writable, nullptr, &timeout) == 1) {
            int error = 0;
            int size = sizeof(error);
            connected = getsockopt(socket, SOL_SOCKET, SO_ERROR, reinterpret_cast<char*>(&error), &size) == 0 && error == 0;
        }
    }
    if (!connected) {
        closesocket(socket);
        return kNoSocket;
    }
    u_long blocking = 0;
    ioctlsocket(socket, FIONBIO, &blocking);
    return socket;
}

}  // namespace

struct TcpConnection::Impl {
    SOCKET socket = kNoSocket;

    ~Impl() {
        if (socket != kNoSocket) {
            closesocket(socket);
        }
    }
};

TcpConnection::TcpConnection(std::unique_ptr<Impl> impl) : impl_(std::move(impl)) {}

TcpConnection::TcpConnection(TcpConnection&& other) noexcept = default;

TcpConnection& TcpConnection::operator=(TcpConnection&& other) noexcept = default;

TcpConnection::~TcpConnection() = default;

std::optional<TcpConnection> TcpConnection::connect(const std::string& host, const std::string& port, int connectTimeoutSeconds,
                                                    int receiveTimeoutSeconds) {
    startWinsock();
    addrinfo hints{};
    hints.ai_family = AF_UNSPEC;
    hints.ai_socktype = SOCK_STREAM;
    addrinfo* addresses = nullptr;
    if (getaddrinfo(host.c_str(), port.c_str(), &hints, &addresses) != 0) {
        return std::nullopt;
    }
    std::unique_ptr<addrinfo, decltype(&freeaddrinfo)> owned(addresses, freeaddrinfo);

    for (const addrinfo* address = addresses; address; address = address->ai_next) {
        const SOCKET socket = connectAddress(address, connectTimeoutSeconds);
        if (socket == kNoSocket) {
            continue;
        }
        auto impl = std::make_unique<Impl>();
        impl->socket = socket;
        DWORD receiveTimeout = static_cast<DWORD>(receiveTimeoutSeconds * 1000);
        setsockopt(socket, SOL_SOCKET, SO_RCVTIMEO, reinterpret_cast<const char*>(&receiveTimeout), sizeof(receiveTimeout));
        BOOL noDelay = TRUE;
        setsockopt(socket, IPPROTO_TCP, TCP_NODELAY, reinterpret_cast<const char*>(&noDelay), sizeof(noDelay));
        return TcpConnection(std::move(impl));
    }
    return std::nullopt;
}

void TcpConnection::send(const std::vector<std::uint8_t>& bytes) {
    std::size_t sent = 0;
    while (sent < bytes.size()) {
        const int count = ::send(impl_->socket, reinterpret_cast<const char*>(bytes.data() + sent), static_cast<int>(bytes.size() - sent), 0);
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
        const int read = ::recv(impl_->socket, reinterpret_cast<char*>(bytes.data() + got), static_cast<int>(count - got), 0);
        if (read <= 0) {
            connectionError();
        }
        got += static_cast<std::size_t>(read);
    }
    return bytes;
}

}  // namespace gwl
