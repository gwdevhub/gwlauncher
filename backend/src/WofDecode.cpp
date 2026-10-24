#include "WofDecode.h"

#include <algorithm>
#include <cstdio>
#include <fstream>
#include <optional>
#include <stdexcept>
#include <string>
#include <vector>

#include "utils/Bytes.h"
#include "utils/Files.h"
#include "utils/Parsing.h"

namespace gwl {

namespace {

// Shifts the way Rust's wrapping_shl does: the count is taken modulo 32, so a count of 32 shifts by nothing.
std::uint32_t shiftLeft(std::uint32_t value, std::uint32_t count) {
    return value << (count & 31);
}

[[noreturn]] void fail(const std::string& message) {
    throw std::runtime_error(message);
}

}  // namespace

BitReader::BitReader(const Bytes& data, std::size_t bytePos) : data_(&data), bitPos_(bytePos * 8) {}

std::size_t BitReader::position() const {
    return bitPos_;
}

std::size_t BitReader::read(std::uint32_t count) {
    std::size_t value = 0;
    for (std::uint32_t i = 0; i < count; ++i) {
        value = (value << 1) | nextBit();
    }
    return value;
}

std::size_t BitReader::alignToBytes() {
    const std::size_t remainder = bitPos_ % 16;
    if (remainder != 0) {
        bitPos_ += 16 - remainder;
    }
    return bitPos_ / 8;
}

std::size_t BitReader::nextBit() {
    const Bytes& data = *data_;
    const std::size_t index = (bitPos_ / 16) * 2;
    std::size_t word = 0;
    if (index + 1 < data.size()) {
        word = static_cast<std::size_t>(data[index]) | static_cast<std::size_t>(data[index + 1]) << 8;
    } else if (index < data.size()) {
        // An odd-length stream ends with its last byte as the high half of a word.
        word = static_cast<std::size_t>(data[index]) << 8;
    }
    const std::size_t bit = (word >> (15 - bitPos_ % 16)) & 1;
    bitPos_ += 1;
    return bit;
}

Huffman::Huffman(const std::vector<std::size_t>& lengths, std::size_t maxLen) : count_(maxLen + 1, 0), maxLen_(maxLen) {
    for (std::size_t length : lengths) {
        if (length > maxLen) {
            fail("Code length " + std::to_string(length) + " exceeds the maximum of " + std::to_string(maxLen) + ".");
        }
        count_[length] += 1;
    }
    count_[0] = 0;

    std::vector<std::size_t> offsets(maxLen + 2, 0);
    for (std::size_t length = 1; length <= maxLen; ++length) {
        offsets[length + 1] = offsets[length] + count_[length];
    }
    symbols_.assign(lengths.size(), 0);
    for (std::size_t symbol = 0; symbol < lengths.size(); ++symbol) {
        const std::size_t length = lengths[symbol];
        if (length != 0) {
            symbols_[offsets[length]] = symbol;
            offsets[length] += 1;
        }
    }
}

std::pair<std::size_t, std::size_t> Huffman::decodeFromBits(std::uint32_t topBits) const {
    std::int64_t code = 0;
    std::int64_t first = 0;
    std::size_t index = 0;
    for (std::size_t length = 1; length <= maxLen_; ++length) {
        code |= static_cast<std::int64_t>((topBits >> (maxLen_ - length)) & 1);
        const std::int64_t count = static_cast<std::int64_t>(count_[length]);
        if (code - first < count && code >= first) {
            return {symbols_[index + static_cast<std::size_t>(code - first)], length};
        }
        index += static_cast<std::size_t>(count);
        first = (first + count) << 1;
        code <<= 1;
    }
    fail("Invalid Huffman codeword.");
}

std::size_t Huffman::decode(BitReader& bits) const {
    std::int64_t code = 0;
    std::int64_t first = 0;
    std::size_t index = 0;
    for (std::size_t length = 1; length <= maxLen_; ++length) {
        code |= static_cast<std::int64_t>(bits.read(1));
        const std::int64_t count = static_cast<std::int64_t>(count_[length]);
        if (code - first < count && code >= first) {
            return symbols_[index + static_cast<std::size_t>(code - first)];
        }
        index += static_cast<std::size_t>(count);
        first = (first + count) << 1;
        code <<= 1;
    }
    fail("Invalid Huffman codeword.");
}

namespace xpress {

const std::size_t kNumSymbols = 512;
const std::size_t kNumChars = 256;
const std::size_t kMaxCodeLen = 15;
const std::size_t kMinMatch = 3;

// wimlib's bit model: a 32-bit left-aligned buffer, refilled 16 bits at a time.
class Reader {
public:
    explicit Reader(const Bytes& data) : data_(data) {}

    void ensure(std::int32_t bits) {
        if (bitsLeft_ >= bits) {
            return;
        }
        if (data_.size() - std::min(next_, data_.size()) < 2) {
            bitsLeft_ = 32;
            return;
        }
        const std::uint32_t word = readU16LE(data_, next_);
        bitBuf_ |= shiftLeft(word, static_cast<std::uint32_t>(16 - bitsLeft_));
        next_ += 2;
        bitsLeft_ += 16;
    }

    std::size_t pop(std::size_t count) {
        if (count == 0) {
            return 0;
        }
        ensure(static_cast<std::int32_t>(count));
        const std::size_t value = bitBuf_ >> (32 - count);
        bitBuf_ = shiftLeft(bitBuf_, static_cast<std::uint32_t>(count));
        bitsLeft_ -= static_cast<std::int32_t>(count);
        return value;
    }

    std::size_t decodeSymbol(const Huffman& huffman) {
        ensure(15);
        const auto [symbol, length] = huffman.decodeFromBits(bitBuf_ >> 17);
        bitBuf_ = shiftLeft(bitBuf_, static_cast<std::uint32_t>(length));
        bitsLeft_ -= static_cast<std::int32_t>(length);
        return symbol;
    }

    std::size_t readRawByte() {
        if (next_ >= data_.size()) {
            return 0;
        }
        return data_[next_++];
    }

    std::size_t readRawU16() {
        if (data_.size() - std::min(next_, data_.size()) < 2) {
            return 0;
        }
        const std::size_t value = readU16LE(data_, next_);
        next_ += 2;
        return value;
    }

private:
    const Bytes& data_;
    std::size_t next_ = 0;
    std::uint32_t bitBuf_ = 0;
    std::int32_t bitsLeft_ = 0;
};

// Decodes one XPRESS Huffman chunk (algorithms 0, 2 and 3). The chunk starts with 512 4-bit code lengths packed into 256 bytes,
// then the Huffman-coded literal and match stream. Ported from wimlib's xpress_decompress.c.
Bytes decode(const Bytes& compressed, std::size_t outSize) {
    if (compressed.size() < kNumSymbols / 2) {
        fail("XPRESS chunk is shorter than its code table.");
    }
    std::vector<std::size_t> lengths(kNumSymbols, 0);
    for (std::size_t i = 0; i < kNumSymbols / 2; ++i) {
        lengths[2 * i] = compressed[i] & 0xF;
        lengths[2 * i + 1] = compressed[i] >> 4;
    }
    const Huffman huffman(lengths, kMaxCodeLen);
    const Bytes stream(compressed.begin() + static_cast<std::ptrdiff_t>(kNumSymbols / 2), compressed.end());
    Reader reader(stream);
    Bytes output(outSize, 0);
    std::size_t written = 0;

    while (written < outSize) {
        const std::size_t symbol = reader.decodeSymbol(huffman);
        if (symbol < kNumChars) {
            output[written++] = static_cast<std::uint8_t>(symbol);
            continue;
        }

        std::size_t length = symbol & 0xF;
        const std::size_t log2Offset = (symbol >> 4) & 0xF;
        reader.ensure(16);
        const std::size_t offset = (std::size_t{1} << log2Offset) | reader.pop(log2Offset);

        if (length == 0xF) {
            length += reader.readRawByte();
            if (length == 0xF + 0xFF) {
                length = reader.readRawU16();
            }
        }
        length += kMinMatch;

        if (offset == 0 || offset > written) {
            fail("XPRESS match points before the start of the chunk.");
        }
        if (written + length > outSize) {
            fail("XPRESS match runs past the end of the chunk.");
        }
        const std::size_t source = written - offset;
        for (std::size_t k = 0; k < length; ++k) {
            output[written] = output[source + k];
            written += 1;
        }
    }

    // The encoder writes symbol 256 after the data as an end marker (MS-XCA).
    if (reader.decodeSymbol(huffman) != kNumChars) {
        fail("XPRESS data is not followed by the end marker.");
    }
    return output;
}

}  // namespace xpress

namespace lzx {

const std::size_t kNumChars = 256;
const std::size_t kNumPrimaryLens = 7;
const std::size_t kMinMatch = 2;
const std::uint32_t kAlignedBits = 3;
const std::size_t kDefaultBlockSize = 32768;
const std::size_t kNumSlots32K = 30;
const std::size_t kLenCodeSymbols = 249;
const std::size_t kPrecodeSymbols = 20;
const std::size_t kMaxCodeLen = 16;
const std::int64_t kWimMagicFileSize = 12000000;

const std::int64_t kOffsetSlotBase[30] = {-2,  -1,  0,    1,    2,    4,    6,    10,   14,   22,   30,   46,   62,   94,   126,
                                          190, 254, 382,  510,  766,  1022, 1534, 2046, 3070, 4094, 6142, 8190, 12286, 16382, 24574};

const std::uint32_t kExtraOffsetBits[30] = {0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13};

std::size_t wrapLength(std::int64_t value) {
    return static_cast<std::size_t>(value < 0 ? value + 17 : value);
}

void fillRun(std::vector<std::size_t>& lens, std::size_t& i, std::size_t last, std::size_t count, std::size_t value) {
    for (std::size_t n = 0; n < count; ++n) {
        if (i >= last) {
            fail("LZX code length run overflows its table.");
        }
        lens[i] = value;
        i += 1;
    }
}

// Reads delta-coded code lengths through a 20-symbol precode, as wimlib's lzx_read_codeword_lens does.
void readCodewordLengths(BitReader& bits, std::vector<std::size_t>& lens, std::size_t first, std::size_t last) {
    std::vector<std::size_t> precodeLens(kPrecodeSymbols, 0);
    for (std::size_t& length : precodeLens) {
        length = bits.read(4);
    }
    const Huffman precode(precodeLens, 16);

    std::size_t i = first;
    while (i < last) {
        const std::size_t symbol = precode.decode(bits);
        if (symbol < 17) {
            lens[i] = wrapLength(static_cast<std::int64_t>(lens[i]) - static_cast<std::int64_t>(symbol));
            i += 1;
        } else if (symbol == 17) {
            const std::size_t count = 4 + bits.read(4);
            fillRun(lens, i, last, count, 0);
        } else if (symbol == 18) {
            const std::size_t count = 20 + bits.read(5);
            fillRun(lens, i, last, count, 0);
        } else {
            const std::size_t count = 4 + bits.read(1);
            const std::size_t z = precode.decode(bits);
            if (z > 17) {
                fail("LZX length run uses an invalid precode symbol.");
            }
            const std::size_t value = wrapLength(static_cast<std::int64_t>(lens[i]) - static_cast<std::int64_t>(z));
            fillRun(lens, i, last, count, value);
        }
    }
}

// Undoes LZX E8 call translation in place, as wimlib's lzx_e8_filter does.
void undoE8Translation(Bytes& data) {
    const std::size_t size = data.size();
    if (size <= 10) {
        return;
    }
    const std::size_t limit = size - 10;
    std::size_t i = 0;
    while (i < limit) {
        if (data[i] != 0xE8) {
            i += 1;
            continue;
        }
        const std::int64_t target = static_cast<std::int32_t>(readU32LE(data, i + 1));
        const auto position = static_cast<std::int64_t>(i);
        std::optional<std::int64_t> translated;
        if (target >= 0) {
            if (target < kWimMagicFileSize) {
                translated = target - position;
            }
        } else if (target >= -position) {
            translated = target + kWimMagicFileSize;
        }
        if (translated) {
            const auto value = static_cast<std::uint32_t>(static_cast<std::int32_t>(*translated));
            for (int k = 0; k < 4; ++k) {
                data[i + 1 + k] = static_cast<std::uint8_t>(value >> (8 * k));
            }
        }
        i += 5;
    }
}

// Decodes a chunk that must produce exactly outSize bytes. Returns the bits the stream used, so the caller can check that the chunk
// was read to its end. Ported from wimlib's lzx_decompress.c and lzx_common.c. A WOF chunk is an independent stream.
std::pair<Bytes, std::size_t> decode(const Bytes& compressed, std::size_t outSize) {
    BitReader bits(compressed, 0);
    std::vector<std::size_t> mainLens(kNumChars + 8 * kNumSlots32K, 0);
    std::vector<std::size_t> lenLens(kLenCodeSymbols, 0);
    std::int64_t recent[3] = {1, 1, 1};
    Bytes output(outSize, 0);
    std::size_t written = 0;

    while (written < outSize) {
        const std::size_t blockType = bits.read(3);
        const std::size_t size = bits.read(1) == 1 ? kDefaultBlockSize : bits.read(16);
        if (size < 1 || written + size > outSize) {
            fail("LZX block size " + std::to_string(size) + " does not fit the chunk.");
        }

        if (blockType == 3) {
            // An uncompressed block: three repeated offsets, then the raw bytes, padded to an even length.
            std::size_t pos = bits.alignToBytes();
            if (compressed.size() < pos + 12) {
                fail("LZX block header is cut off.");
            }
            for (int i = 0; i < 3; ++i) {
                recent[i] = static_cast<std::int64_t>(readU32LE(compressed, pos + 4 * i));
            }
            pos += 12;
            if (compressed.size() < pos + size) {
                fail("LZX uncompressed block is cut off.");
            }
            std::copy(compressed.begin() + static_cast<std::ptrdiff_t>(pos), compressed.begin() + static_cast<std::ptrdiff_t>(pos + size),
                      output.begin() + static_cast<std::ptrdiff_t>(written));
            written += size;
            pos += size + (size & 1);
            bits = BitReader(compressed, pos);
            continue;
        }

        std::optional<Huffman> aligned;
        if (blockType == 2) {
            std::vector<std::size_t> alignedLens(8, 0);
            for (std::size_t& length : alignedLens) {
                length = bits.read(kAlignedBits);
            }
            aligned.emplace(alignedLens, 7);
        }

        readCodewordLengths(bits, mainLens, 0, kNumChars);
        readCodewordLengths(bits, mainLens, kNumChars, kNumChars + 8 * kNumSlots32K);
        const Huffman main(mainLens, kMaxCodeLen);
        readCodewordLengths(bits, lenLens, 0, kLenCodeSymbols);
        const Huffman lens(lenLens, kMaxCodeLen);

        const std::size_t end = written + size;
        while (written < end) {
            const std::size_t symbol = main.decode(bits);
            if (symbol < kNumChars) {
                output[written++] = static_cast<std::uint8_t>(symbol);
                continue;
            }

            const std::size_t s = symbol - kNumChars;
            std::size_t length = s % 8;
            const std::size_t slot = s / 8;
            if (length == kNumPrimaryLens) {
                length += lens.decode(bits);
            }
            length += kMinMatch;

            std::int64_t offset = 0;
            if (slot < 3) {
                offset = recent[slot];
                recent[slot] = recent[0];
            } else {
                const std::uint32_t extra = kExtraOffsetBits[slot];
                std::int64_t value = 0;
                if (aligned && blockType == 2 && extra >= kAlignedBits) {
                    value = static_cast<std::int64_t>(bits.read(extra - kAlignedBits) << kAlignedBits) |
                            static_cast<std::int64_t>(aligned->decode(bits));
                } else {
                    value = static_cast<std::int64_t>(bits.read(extra));
                }
                offset = kOffsetSlotBase[slot] + value;
                recent[2] = recent[1];
                recent[1] = recent[0];
            }
            recent[0] = offset;

            if (offset <= 0 || static_cast<std::size_t>(offset) > written) {
                fail("LZX match points before the start of the chunk.");
            }
            if (written + length > end) {
                fail("LZX match runs past the end of its block.");
            }
            const std::size_t source = written - static_cast<std::size_t>(offset);
            for (std::size_t k = 0; k < length; ++k) {
                output[written] = output[source + k];
                written += 1;
            }
        }
    }

    undoE8Translation(output);
    return {output, bits.position()};
}

}  // namespace lzx

namespace wof {

std::size_t chunkSize(std::uint32_t algorithm) {
    switch (algorithm) {
        case 0:
            return 4096;
        case 1:
            return 32768;
        case 2:
            return 8192;
        case 3:
            return 16384;
        default:
            fail("Unsupported WOF algorithm " + std::to_string(algorithm) + ".");
    }
}

Bytes decodeChunk(const Bytes& raw, std::size_t expect, std::uint32_t algorithm, std::size_t chunk) {
    try {
        if (algorithm == 1) {
            auto [decoded, bitsUsed] = lzx::decode(raw, expect);
            // LZX must use its chunk to within padding; a larger gap means the chunk was misread.
            const auto unused = static_cast<std::int64_t>((raw.size() + (raw.size() & 1)) * 8) - static_cast<std::int64_t>(bitsUsed);
            if (unused < 0 || unused > 16) {
                fail("the chunk did not decode cleanly");
            }
            return decoded;
        }
        return xpress::decode(raw, expect);
    } catch (const std::runtime_error& error) {
        fail("Chunk " + std::to_string(chunk) + " could not be decompressed: " + error.what());
    }
}

namespace {

// The number after label, in text after it. Nothing when there's no label or no digits.
std::optional<std::size_t> numberAfter(const std::string& text, const std::string& label) {
    const std::size_t at = text.find(label);
    if (at == std::string::npos) {
        return std::nullopt;
    }
    std::size_t i = at + label.size();
    while (i < text.size() && (text[i] == ' ' || text[i] == '\t')) {
        i += 1;
    }
    std::size_t end = i;
    while (end < text.size() && text[end] >= '0' && text[end] <= '9') {
        end += 1;
    }
    std::size_t number;
    if (!stringToSize(text.substr(i, end - i), &number)) {
        return std::nullopt;
    }
    return number;
}

// The text between each occurrence of marker and the next one, as Rust's split(...).skip(1) gives it.
std::vector<std::string> blocksAfter(const std::string& text, const std::string& marker) {
    std::vector<std::string> blocks;
    std::size_t at = text.find(marker);
    while (at != std::string::npos) {
        const std::size_t start = at + marker.size();
        const std::size_t next = text.find(marker, start);
        blocks.push_back(text.substr(start, next == std::string::npos ? std::string::npos : next - start));
        at = next;
    }
    return blocks;
}

}  // namespace

std::pair<std::size_t, std::size_t> parseHeader(const std::string& header) {
    const std::size_t compressedAt = header.find("'WofCompressedData'");
    if (compressedAt == std::string::npos) {
        fail("The file has no compressed data stream (is it really compacted?).");
    }
    const auto streamSize = numberAfter(header.substr(compressedAt), "Data size:");
    if (!streamSize) {
        fail("The compressed data stream has no size.");
    }
    for (const std::string& block : blocksAfter(header, "Dumping attribute $DATA")) {
        if (block.find("Attribute name:") != std::string::npos) {
            continue;
        }
        if (const auto uncompressed = numberAfter(block, "Data size:")) {
            return {*uncompressed, *streamSize};
        }
    }
    fail("Could not read the file's uncompressed size.");
}

namespace {

std::string megabytes(std::uint64_t bytes) {
    return std::to_string(bytes / 1048576);
}

}  // namespace

void decodeStream(const ReadExact& readExact, std::uint32_t algorithm, const std::filesystem::path& work, std::uint64_t freeBytes,
                  const ProgressReport& report, const std::atomic<bool>& cancel) {
    // The volume info is text up to a marker line.
    std::string header;
    std::string line;
    for (;;) {
        char byte = 0;
        if (!readExact(&byte, 1)) {
            fail("The privileged read ended before the volume info was printed. Was the password prompt cancelled?");
        }
        if (byte != '\n') {
            line.push_back(byte);
            continue;
        }
        while (!line.empty() && (line.back() == '\r' || line.back() == ' ' || line.back() == '\t')) {
            line.pop_back();
        }
        if (line == "@@WOFSTREAM@@") {
            break;
        }
        header += line + "\n";
        line.clear();
    }
    const auto [uncompressed, streamSize] = parseHeader(header);

    if (freeBytes < uncompressed + uncompressed / 50) {
        fail("Not enough free space: about " + megabytes(uncompressed) + " MB is needed, " + megabytes(freeBytes) + " MB is free.");
    }

    const std::size_t chunk = chunkSize(algorithm);
    const std::size_t chunkCount = (uncompressed + chunk - 1) / chunk;
    const std::size_t tableBytes = chunkCount > 0 ? (chunkCount - 1) * 4 : 0;
    Bytes table(tableBytes, 0);
    if (tableBytes > 0 && !readExact(table.data(), tableBytes)) {
        fail("The compressed data is cut off: the chunk table ended early.");
    }
    std::vector<std::size_t> ends(chunkCount, 0);
    for (std::size_t i = 0; i + 1 < chunkCount; ++i) {
        ends[i] = readU32LE(table, 4 * i);
    }
    if (chunkCount > 0) {
        if (streamSize < tableBytes) {
            fail("The compressed stream is smaller than its table.");
        }
        ends[chunkCount - 1] = streamSize - tableBytes;
    }

    std::ofstream output(work, std::ios::binary | std::ios::trunc);
    if (!output) {
        fail("Could not write " + pathToUtf8(work));
    }
    std::size_t start = 0;
    for (std::size_t i = 0; i < chunkCount; ++i) {
        if (cancel.load()) {
            fail("Cancelled.");
        }
        const std::size_t expect = i + 1 < chunkCount ? chunk : uncompressed - (chunkCount - 1) * chunk;
        if (ends[i] < start) {
            fail("The chunk table goes backwards.");
        }
        const std::size_t size = ends[i] - start;
        start = ends[i];

        Bytes bytes;
        if (size == 0) {
            bytes.assign(expect, 0);
        } else {
            Bytes raw(size, 0);
            if (!readExact(raw.data(), size)) {
                fail("The compressed data is cut off: the chunk ended early.");
            }
            bytes = size == expect ? raw : decodeChunk(raw, expect, algorithm, i);
        }
        output.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));

        if (i % 64 == 0 || i + 1 == chunkCount) {
            const std::uint64_t doneBytes = std::min<std::uint64_t>((i + 1) * chunk, uncompressed);
            report(megabytes(doneBytes) + " of " + megabytes(uncompressed) + " MB",
                   static_cast<double>(i) / static_cast<double>(chunkCount));
        }
    }
    if (!output) {
        fail("Could not write " + pathToUtf8(work));
    }
}

}  // namespace wof
}  // namespace gwl
