// The decoders for Windows-compacted (WOF) data: XPRESS and LZX chunks, and the chunk table that holds them. A port of the Rust
// launcher's wof decoders, which follow wimlib. Platform-free, so it's tested on every platform.

#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <functional>
#include <string>
#include <utility>
#include <vector>

#include "Progress.h"
#include "utils/Strings.h"

namespace gwl {

// Reads bits from 16-bit little-endian words, most significant bit first. Reads past the end give zeros.
class BitReader {
public:
    BitReader(const Bytes& data, std::size_t bytePos);

    std::size_t position() const;
    std::size_t read(std::uint32_t count);
    // Skips to the next 16-bit boundary and returns the byte offset there.
    std::size_t alignToBytes();

private:
    std::size_t nextBit();

    const Bytes* data_;
    std::size_t bitPos_;
};

// Canonical Huffman decoding. Codes are assigned in symbol order within each length.
class Huffman {
public:
    // Throws std::runtime_error when a length is longer than maxLen.
    Huffman(const std::vector<std::size_t>& lengths, std::size_t maxLen);

    // Decodes from the top maxLen bits of topBits, right-aligned. Returns the symbol and its code length.
    std::pair<std::size_t, std::size_t> decodeFromBits(std::uint32_t topBits) const;
    std::size_t decode(BitReader& bits) const;

private:
    std::vector<std::size_t> count_;
    std::vector<std::size_t> symbols_;
    std::size_t maxLen_;
};

namespace wof {

// The WOF algorithm's chunk size: 0 XPRESS 4K, 1 LZX 32K, 2 XPRESS 8K, 3 XPRESS 16K. Throws for any other algorithm.
std::size_t chunkSize(std::uint32_t algorithm);

// One chunk, decoded to exactly expect bytes. Throws std::runtime_error when the chunk is corrupt.
Bytes decodeChunk(const Bytes& raw, std::size_t expect, std::uint32_t algorithm, std::size_t chunk);

// The uncompressed size and the compressed stream's size, from ntfsinfo's text. Throws when either is missing.
std::pair<std::size_t, std::size_t> parseHeader(const std::string& header);

// The compressed data as a stream of bytes. Reads exactly count bytes, or returns false when the stream ends first.
using ReadExact = std::function<bool(void* out, std::size_t count)>;

// Decodes the compressed stream into the file at work. The volume info comes first, then the chunk table, then the chunks.
// Throws std::runtime_error with the reason, or with "Cancelled." when cancel is set.
void decodeStream(const ReadExact& readExact, std::uint32_t algorithm, const std::filesystem::path& work,
                  std::uint64_t freeBytes, const ProgressReport& report, const std::atomic<bool>& cancel);

}  // namespace wof
}  // namespace gwl
