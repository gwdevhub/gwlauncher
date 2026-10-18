// Decodes the LZ77 + Huffman stream ArenaNet serves for Gw.exe. A port of the Rust launcher's gw_huffman, which follows the C#
// BitStream, HuffmanTable and GuildwarsExeDecoder: the shifts wrap the way C# shifts do, so the tables and bit order must match.

#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <vector>

namespace gwl {

// Decodes the compressed file to its expected size. progress gets the fraction written, from 0 to 1. Throws std::runtime_error
// when the stream is corrupt, or with kCancelledMessage when cancel is set.
std::vector<std::uint8_t> decompressGw(const std::vector<std::uint8_t>& input, std::size_t expected,
                                       const std::function<void(double)>& progress, const std::atomic<bool>& cancel);

}  // namespace gwl
