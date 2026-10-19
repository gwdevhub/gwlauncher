// The ArenaNet file server: the handshake, the latest Gw.exe's details, and its compressed download. A port of the Rust launcher's
// gw_net, which follows the C# GuildwarsClient, GuildwarsFileStream and GuildwarsExeDownloader.

#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <vector>

#include "Network.h"
#include "Progress.h"

namespace gwl {

// The file details the server sends for a file: its id, sizes and checksum.
struct GwFileInfo {
    std::int32_t fileId = 0;
    std::int32_t sizeDecompressed = 0;
    std::int32_t sizeCompressed = 0;
    std::int32_t crc = 0;
};

// The details of the latest Gw.exe. Throws std::runtime_error when ArenaNet can't be reached or doesn't answer.
GwFileInfo latestGwInfo();

// The same details, asked of ArenaNet at most once in a few minutes, so a round of checks over many accounts makes one request. A
// failed request isn't kept, so the next call asks again. Throws like latestGwInfo.
GwFileInfo cachedLatestGwInfo();

// Forgets the cached answer, so the next cachedLatestGwInfo asks ArenaNet again.
void clearCachedLatestGwInfo();

// The compressed Gw.exe, and the size it decompresses to.
struct GwDownload {
    std::vector<std::uint8_t> compressed;
    std::size_t decompressedSize = 0;
};

// A connection to a file server that has answered for the latest Gw.exe. Its details come with the answer, and the download uses this
// same connection, so the file isn't requested over a second handshake.
struct GwOpenDownload {
    TcpConnection connection;
    GwFileInfo info;
};

// The CRC the file server sends for a file: the CRC-32 of its decompressed contents, continued over the file id's four bytes.
std::uint32_t gwFileCrc(const std::vector<std::uint8_t>& decompressed, std::int32_t fileId);

// Connects and asks for the latest Gw.exe's details. Throws std::runtime_error when ArenaNet can't be reached or doesn't answer.
GwOpenDownload openLatestGw();

// Downloads the open file's compressed bytes on its connection. progress gets the bytes received and the total.
GwDownload downloadOpened(GwOpenDownload& open, const std::atomic<bool>& cancel, const std::function<void(std::size_t, std::size_t)>& progress);

}  // namespace gwl
