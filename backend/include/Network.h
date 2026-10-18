// A TCP connection to a server, for the protocols the launcher speaks. Each platform implements it in platform/<os>/Network.cpp,
// with POSIX sockets on Linux and Winsock on Windows.

#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <vector>

namespace gwl {

class TcpConnection {
public:
    // Connects to the first address of host:port that answers within connectTimeoutSeconds. Reads then give up after
    // receiveTimeoutSeconds. Returns nothing when no address answers.
    static std::optional<TcpConnection> connect(const std::string& host, const std::string& port, int connectTimeoutSeconds,
                                                int receiveTimeoutSeconds);

    TcpConnection(TcpConnection&& other) noexcept;
    TcpConnection& operator=(TcpConnection&& other) noexcept;
    TcpConnection(const TcpConnection&) = delete;
    TcpConnection& operator=(const TcpConnection&) = delete;
    ~TcpConnection();

    // Sends every byte. Throws std::runtime_error when the connection fails.
    void send(const std::vector<std::uint8_t>& bytes);
    // Reads exactly count bytes. Throws std::runtime_error when the connection closes or times out first.
    std::vector<std::uint8_t> receive(std::size_t count);

private:
    struct Impl;
    explicit TcpConnection(std::unique_ptr<Impl> impl);
    std::unique_ptr<Impl> impl_;
};

}  // namespace gwl
