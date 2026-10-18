#include "GwNet.h"

#include <chrono>
#include <cstdio>
#include <mutex>
#include <optional>
#include <stdexcept>
#include <string>
#include <utility>

#include "Network.h"
#include "utils/Bytes.h"
#include "utils/Crc.h"

namespace gwl {

namespace {

const char* const kPort = "6112";
const int kConnectTimeoutSeconds = 10;
const int kReceiveTimeoutSeconds = 5;

// Sends the handshake and returns the manifest's id for the latest Gw.exe.
std::int32_t handshake(TcpConnection& connection) {
    std::vector<std::uint8_t> request;
    request.push_back(1);
    appendU32LE(request, 0);
    appendU16LE(request, 0xF1);
    appendU16LE(request, 0x10);
    appendU32LE(request, 1);
    appendU32LE(request, 0);
    appendU32LE(request, 0);
    connection.send(request);
    // The manifest is 32 bytes; its last field is the latest Gw.exe.
    const std::vector<std::uint8_t> manifest = connection.receive(32);
    return static_cast<std::int32_t>(readU32LE(manifest, 28));
}

// Connects to the first file server that answers the handshake, and returns the connection with the latest Gw.exe's id.
std::pair<TcpConnection, std::int32_t> connect() {
    for (int i = 1; i < 13; ++i) {
        const std::string host = "file" + std::to_string(i) + ".arenanetworks.com";
        auto connection = TcpConnection::connect(host, kPort, kConnectTimeoutSeconds, kReceiveTimeoutSeconds);
        if (!connection) {
            continue;
        }
        try {
            const std::int32_t latest = handshake(*connection);
            return {std::move(*connection), latest};
        } catch (const std::runtime_error&) {
            // This server didn't answer the handshake, so the next one is tried.
        }
    }
    throw std::runtime_error("Failed to connect to ArenaNet servers");
}

// Asks for a file and reads its details. The server answers with a metadata message first, then the details.
GwFileInfo requestFile(TcpConnection& connection, std::int32_t fileId) {
    std::vector<std::uint8_t> request;
    appendU16LE(request, 0x3F2);
    appendU16LE(request, 0xC);
    appendU32LE(request, static_cast<std::uint32_t>(fileId));
    appendU32LE(request, 0);
    connection.send(request);

    const std::vector<std::uint8_t> metadata = connection.receive(4);
    const std::uint16_t header = readU16LE(metadata, 0);
    if (header == 0x4F2) {
        throw std::runtime_error("Error 0x4F2: Could not find file");
    }
    if (header != 0x5F2) {
        char text[64];
        std::snprintf(text, sizeof(text), "Error: Unexpected field response; expected 0x5F2, got 0x%X", header);
        throw std::runtime_error(text);
    }

    const std::vector<std::uint8_t> details = connection.receive(16);
    GwFileInfo info;
    info.fileId = static_cast<std::int32_t>(readU32LE(details, 0));
    info.sizeDecompressed = static_cast<std::int32_t>(readU32LE(details, 4));
    info.sizeCompressed = static_cast<std::int32_t>(readU32LE(details, 8));
    info.crc = static_cast<std::int32_t>(readU32LE(details, 12));
    return info;
}

// The server streams data after a request, so one request covers a whole window. The next window is requested once the
// current one has arrived. 0x10000 is the largest size ArenaNet accepts; larger requests close the connection.
const std::uint32_t kWindowBytes = 0x10000;

void requestWindow(TcpConnection& connection) {
    std::vector<std::uint8_t> request;
    appendU16LE(request, 0x7F3);
    appendU16LE(request, 0x8);
    appendU32LE(request, kWindowBytes);
    connection.send(request);
}

}  // namespace

std::uint32_t gwFileCrc(const std::vector<std::uint8_t>& decompressed, std::int32_t fileId) {
    Bytes id;
    appendU32LE(id, static_cast<std::uint32_t>(fileId));
    return crc32Update(crc32Update(0, decompressed), id);
}

GwOpenDownload openLatestGw() {
    auto [connection, latest] = connect();
    GwFileInfo info = requestFile(connection, latest);
    return GwOpenDownload{std::move(connection), info};
}

GwFileInfo latestGwInfo() {
    return openLatestGw().info;
}

namespace {

// The cached answer. The mutex is held while ArenaNet is asked, so a round of checks that all miss the cache makes one request.
std::mutex cacheMutex;
std::optional<GwFileInfo> cachedInfo;
std::chrono::steady_clock::time_point cachedAt;

}  // namespace

GwFileInfo cachedLatestGwInfo() {
    const std::chrono::minutes lifetime{5};
    std::lock_guard<std::mutex> lock(cacheMutex);
    const auto now = std::chrono::steady_clock::now();
    if (cachedInfo && now - cachedAt < lifetime) {
        return *cachedInfo;
    }
    cachedInfo = latestGwInfo();
    cachedAt = now;
    return *cachedInfo;
}

void clearCachedLatestGwInfo() {
    std::lock_guard<std::mutex> lock(cacheMutex);
    cachedInfo.reset();
}

GwDownload downloadOpened(GwOpenDownload& open, const std::atomic<bool>& cancel, const std::function<void(std::size_t, std::size_t)>& progress) {
    TcpConnection& connection = open.connection;
    const GwFileInfo& info = open.info;
    const std::size_t total = info.sizeCompressed > 0 ? static_cast<std::size_t>(info.sizeCompressed) : 0;

    GwDownload download;
    download.compressed.reserve(total);
    if (total > 0) {
        requestWindow(connection);
    }
    // Bytes received since the last window was requested. Once a full window has arrived, the next one is asked for.
    std::size_t sinceRequest = 0;
    while (download.compressed.size() < total) {
        if (cancel.load()) {
            throw std::runtime_error(kCancelledMessage);
        }

        const std::vector<std::uint8_t> metadata = connection.receive(4);
        const std::uint16_t header = readU16LE(metadata, 0);
        if (header != 0x6F2 && header != 0x6F3) {
            char text[64];
            std::snprintf(text, sizeof(text), "Unknown header in response %X", header);
            throw std::runtime_error(text);
        }
        // The chunk length includes the 4-byte header that was just read.
        const std::int64_t length = static_cast<std::int16_t>(readU16LE(metadata, 2)) - 4;
        if (length <= 0) {
            throw std::runtime_error("ArenaNet sent an empty chunk.");
        }
        const std::size_t chunkSize = static_cast<std::size_t>(length);

        const std::vector<std::uint8_t> chunk = connection.receive(chunkSize);
        download.compressed.insert(download.compressed.end(), chunk.begin(), chunk.end());
        progress(download.compressed.size(), total);

        sinceRequest += chunkSize;
        if (sinceRequest >= kWindowBytes && download.compressed.size() < total) {
            requestWindow(connection);
            sinceRequest -= kWindowBytes;
        }
    }
    download.decompressedSize = info.sizeDecompressed > 0 ? static_cast<std::size_t>(info.sizeDecompressed) : 0;
    return download;
}

}  // namespace gwl
