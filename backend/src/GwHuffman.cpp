#include "GwHuffman.h"

#include <cstddef>
#include <iterator>
#include <stdexcept>
#include <string>
#include <utility>

#include "GwNet.h"

namespace gwl {

namespace {

const std::uint32_t kEmpty = 0xFFFF'FFFF;

const std::pair<std::uint32_t, std::uint32_t> kTable1[] = {
    {0xa000'0000, 2}, {0x6000'0000, 6}, {0x4000'0000, 10}, {0x2000'0000, 18}, {0x1200'0000, 25}, {0x0c00'0000, 31},
    {0x0700'0000, 41}, {0x0300'0000, 57}, {0x0160'0000, 70}, {0x00f0'0000, 77}, {0x00c0'0000, 83}, {0x00b0'0000, 87},
    {0x00a0'0000, 95}, {0x0000'0000, 255},
};

const std::uint32_t kTable2[256] = {
    0x08, 0x09, 0x0A, 0x00, 0x07, 0x0B, 0x0C, 0x06, 0x29, 0x2A, 0xE0, 0x04, 0x05, 0x20, 0x28, 0x2B, 0x2C, 0x40, 0x4A, 0x03,
    0x0D, 0x25, 0x26, 0x27, 0x48, 0x49, 0x24, 0x47, 0x4B, 0x4C, 0x69, 0x6A, 0x23, 0x46, 0x60, 0x63, 0x67, 0x68, 0x88, 0x89,
    0xA0, 0xE8, 0x01, 0x02, 0x2D, 0x43, 0x44, 0x45, 0x65, 0x66, 0x80, 0x87, 0x8A, 0xA8, 0xA9, 0xC0, 0xC9, 0xE9, 0x0E, 0x4D,
    0x64, 0x6B, 0x6C, 0x84, 0x85, 0x8B, 0xA4, 0xA5, 0xAA, 0xC8, 0xE5, 0x83, 0x86, 0xA6, 0xA7, 0xC7, 0xCA, 0xE7, 0x22, 0x2E,
    0x8C, 0xC4, 0xE4, 0xE6, 0x4E, 0x6D, 0xC6, 0xEC, 0x0F, 0x10, 0x11, 0x8D, 0xAB, 0xAC, 0xCC, 0xEA, 0x12, 0x13, 0x14, 0x15,
    0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x21, 0x2F, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
    0x38, 0x39, 0x3A, 0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x41, 0x42, 0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
    0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x61, 0x62, 0x6E, 0x6F, 0x70, 0x71, 0x72, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
    0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F, 0x81, 0x82, 0x8E, 0x8F, 0x90, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
    0x99, 0x9A, 0x9B, 0x9C, 0x9D, 0x9E, 0x9F, 0xA1, 0xA2, 0xA3, 0xAD, 0xAE, 0xAF, 0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
    0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD, 0xBE, 0xBF, 0xC1, 0xC2, 0xC3, 0xC5, 0xCB, 0xCD, 0xCE, 0xCF, 0xD0, 0xD1, 0xD2,
    0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xDB, 0xDC, 0xDD, 0xDE, 0xDF, 0xE1, 0xE2, 0xE3, 0xEB, 0xED, 0xEE, 0xEF,
    0xF0, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFB, 0xFC, 0xFD, 0xFE, 0xFF,
};

const std::uint32_t kTable3[32] = {
    0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x0A, 0x0C, 0x0E, 0x10, 0x14, 0x18, 0x1C, 0x20, 0x28, 0x30, 0x38,
    0x40, 0x50, 0x60, 0x70, 0x80, 0xA0, 0xC0, 0xE0, 0xFF, 0x00, 0x00, 0x00,
};

const std::uint32_t kExtraBitsLength[29] = {
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x01, 0x02, 0x02, 0x02, 0x02, 0x03, 0x03, 0x03, 0x03,
    0x04, 0x04, 0x04, 0x04, 0x05, 0x05, 0x05, 0x05, 0x00,
};

const std::uint32_t kExtraBitsDistance[32] = {
    0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x02, 0x02, 0x03, 0x03, 0x04, 0x04, 0x05, 0x05, 0x06, 0x06, 0x07, 0x07, 0x08, 0x08,
    0x09, 0x09, 0x0A, 0x0A, 0x0B, 0x0B, 0x0C, 0x0C, 0x0D, 0x0D, 0x0E, 0x0E,
};

const std::uint32_t kBacktrackTable[46] = {
    0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0006, 0x0008, 0x000C, 0x0010, 0x0018, 0x0020, 0x0030, 0x0040, 0x0060, 0x0080,
    0x00C0, 0x0100, 0x0180, 0x0200, 0x0300, 0x0400, 0x0600, 0x0800, 0x0C00, 0x1000, 0x1800, 0x2000, 0x3000, 0x4000, 0x6000,
    0x0100, 0x0302, 0x0504, 0x0706, 0x0A08, 0x0E0C, 0x1410, 0x1C18, 0x2820, 0x3830, 0x5040, 0x7060, 0xA080, 0xE0C0, 0x00FF,
    0x0000,
};

// C# and Rust shift counts are taken modulo 32, so a count of 32 shifts by nothing.
std::uint32_t shr(std::uint32_t value, std::uint32_t count) {
    return value >> (count & 31);
}

std::uint32_t shl(std::uint32_t value, std::uint32_t count) {
    return value << (count & 31);
}

[[noreturn]] void corrupt() {
    throw std::runtime_error("Downloaded Gw.exe is corrupt.");
}

[[noreturn]] void badTable() {
    throw std::runtime_error("Downloaded Gw.exe has a bad code table.");
}

// Reads the stream a bit at a time, most significant bit first, refilling 32 bits at a time.
class BitStream {
public:
    explicit BitStream(const std::vector<std::uint8_t>& input) : input_(input) {
        if (input.size() < 8) {
            throw std::runtime_error("Downloaded Gw.exe is too short.");
        }
        buf1_ = word(0);
        buf2_ = word(4);
    }

    std::uint32_t peek(std::uint32_t count) const { return shr(buf1_, 32 - count); }

    std::uint32_t read(std::uint32_t count) {
        const std::uint32_t value = peek(count);
        consume(count);
        return value;
    }

    void consume(std::uint32_t count) {
        buf1_ = shl(buf1_, count) | shr(buf2_, 32 - count);
        const std::int64_t bits = count;
        if (avail_ < bits) {
            if (idx_ >= input_.size()) {
                avail_ = 0;
                buf2_ = 0;
            } else {
                if (idx_ + 4 > input_.size()) {
                    throw std::runtime_error("Downloaded Gw.exe ended early.");
                }
                buf2_ = word(idx_);
                idx_ += 4;
                const std::int64_t newAvail = avail_ + 32 - bits;
                buf1_ = buf1_ + shr(buf2_, static_cast<std::uint32_t>(newAvail));
                buf2_ = shl(buf2_, static_cast<std::uint32_t>(bits - avail_));
                avail_ = newAvail;
            }
        } else {
            avail_ -= bits;
            buf2_ = shl(buf2_, count);
        }
    }

private:
    std::uint32_t word(std::size_t at) const {
        return static_cast<std::uint32_t>(input_[at]) | static_cast<std::uint32_t>(input_[at + 1]) << 8 |
               static_cast<std::uint32_t>(input_[at + 2]) << 16 | static_cast<std::uint32_t>(input_[at + 3]) << 24;
    }

    const std::vector<std::uint8_t>& input_;
    std::size_t idx_ = 8;
    std::uint32_t buf1_ = 0;
    std::uint32_t buf2_ = 0;
    std::int64_t avail_ = 32;
};

// The code lengths and values for one alphabet: short codes in a lookup table, long codes in groups.
class HuffmanTable {
public:
    static HuffmanTable build(BitStream& stream) {
        std::uint32_t root[32];
        for (std::uint32_t& entry : root) {
            entry = kEmpty;
        }
        const std::uint32_t symbolCount = stream.read(16);
        std::vector<std::int32_t> follow(symbolCount, 0);
        std::int64_t total = 0;
        std::int64_t symbolIdx = static_cast<std::int64_t>(symbolCount) - 1;
        while (symbolIdx != -1) {
            if (symbolIdx < -1) {
                badTable();
            }
            const std::uint32_t buf1 = stream.peek(32);
            std::size_t entry = 0;
            while (entry < std::size(kTable1) && kTable1[entry].first > buf1) {
                ++entry;
            }
            if (entry == std::size(kTable1)) {
                badTable();
            }
            const std::uint32_t bitCount = static_cast<std::uint32_t>(entry) + 3;
            const std::int64_t offset = shr(buf1 - kTable1[entry].first, 32 - bitCount);
            stream.consume(bitCount);

            const std::int64_t index = static_cast<std::int64_t>(kTable1[entry].second) - offset;
            if (index < 0 || index >= 256) {
                badTable();
            }
            const std::uint32_t temp = kTable2[index];
            std::int64_t number = temp >> 5;
            const std::size_t symbolLen = temp & 0x1F;
            if (symbolLen != 0 || symbolCount < 2) {
                number += 1;
                total += number;
                for (std::int64_t i = 0; i < number; ++i) {
                    if (symbolIdx < 0) {
                        badTable();
                    }
                    follow[static_cast<std::size_t>(symbolIdx)] = static_cast<std::int32_t>(root[symbolLen]);
                    root[symbolLen] = static_cast<std::uint32_t>(symbolIdx);
                    symbolIdx -= 1;
                }
            } else {
                symbolIdx -= number + 1;
            }
        }

        if (total == 0) {
            if (symbolCount == 0) {
                badTable();
            }
            follow[symbolCount - 1] = static_cast<std::int32_t>(root[0]);
            root[0] = symbolCount - 1;
            total = 1;
        }

        HuffmanTable table;
        table.largeTranslation.assign(24, {0, 0, 0});
        std::int64_t nextBits = 1;
        std::int64_t inTable = 0;
        for (std::uint32_t encLen = 1; encLen <= 8; ++encLen) {
            std::uint32_t current = root[encLen];
            while (current != kEmpty) {
                if (current >= symbolCount || nextBits >= (std::int64_t{1} << encLen)) {
                    badTable();
                }
                const std::size_t first = static_cast<std::size_t>(nextBits << (8 - encLen));
                const std::size_t count = std::size_t{1} << (8 - encLen);
                if (first + count > std::size(table.nodes)) {
                    badTable();
                }
                for (std::size_t i = first; i < first + count; ++i) {
                    table.nodes[i] = {encLen, current};
                }
                current = static_cast<std::uint32_t>(follow[current]);
                inTable += 1;
                nextBits -= 1;
            }
            nextBits = (nextBits << 1) + 1;
        }

        if (inTable == total) {
            return table;
        }

        for (std::uint32_t encLen = 9; encLen < 32; ++encLen) {
            std::uint32_t current = root[encLen];
            while (current != kEmpty) {
                if (current >= symbolCount || nextBits >= (std::int64_t{1} << encLen)) {
                    badTable();
                }
                const std::size_t partial = static_cast<std::size_t>(nextBits >> (encLen - 8));
                if (partial >= std::size(table.nodes)) {
                    badTable();
                }
                table.nodes[partial] = {kEmpty, 0};
                table.largeValues.push_back(current);
                current = static_cast<std::uint32_t>(follow[current]);
                nextBits -= 1;
            }
            const std::uint32_t first = shl(static_cast<std::uint32_t>(static_cast<std::int32_t>(nextBits + 1)), 32 - encLen);
            const std::int64_t last = static_cast<std::int64_t>(table.largeValues.size()) - 1;
            table.largeTranslation[encLen - 9] = {first, last, encLen};
            nextBits = (nextBits << 1) + 1;
        }
        return table;
    }

    std::uint32_t nextCode(BitStream& stream) const {
        const std::uint32_t bits = stream.peek(8);
        std::uint32_t encLen = nodes[bits].first;
        std::uint32_t encVal = nodes[bits].second;
        if (encLen == kEmpty) {
            const std::uint32_t buf1 = stream.peek(32);
            const LargeEntry* found = nullptr;
            for (const LargeEntry& entry : largeTranslation) {
                if (entry.first <= buf1) {
                    found = &entry;
                    break;
                }
            }
            if (!found) {
                badTable();
            }
            encLen = found->length;
            const std::int64_t group = shr(buf1 - found->first, 32 - found->length);
            const std::int64_t index = found->last - group;
            if (index < 0 || static_cast<std::size_t>(index) >= largeValues.size()) {
                badTable();
            }
            encVal = largeValues[static_cast<std::size_t>(index)];
        }
        stream.consume(encLen);
        return encVal;
    }

private:
    struct LargeEntry {
        std::uint32_t first;
        std::int64_t last;
        std::uint32_t length;
    };

    std::pair<std::uint32_t, std::uint32_t> nodes[256] = {};
    std::vector<LargeEntry> largeTranslation;
    std::vector<std::uint32_t> largeValues;
};

}  // namespace

std::vector<std::uint8_t> decompressGw(const std::vector<std::uint8_t>& input, std::size_t expected,
                                       const std::function<void(double)>& progress, const std::atomic<bool>& cancel) {
    const std::uint32_t extraBitsLengthSize = std::size(kExtraBitsLength);
    BitStream bits(input);
    bits.consume(4);
    const std::uint32_t first4 = bits.read(4);

    std::vector<std::uint8_t> output(expected, 0);
    std::size_t len = 0;
    while (len < expected) {
        if (cancel.load()) {
            throw std::runtime_error(kCancelledMessage);
        }
        const HuffmanTable literals = HuffmanTable::build(bits);
        const HuffmanTable distances = HuffmanTable::build(bits);
        const std::size_t blockSize = (static_cast<std::size_t>(bits.read(4)) + 1) * 4096;
        std::size_t symbols = 0;
        while (symbols < blockSize && len < expected) {
            symbols += 1;
            const std::uint32_t code = literals.nextCode(bits);
            if (code < 0x100) {
                output[len] = static_cast<std::uint8_t>(code);
                len += 1;
                continue;
            }

            const std::size_t index = code - 256;
            if (index >= extraBitsLengthSize || index >= std::size(kTable3)) {
                corrupt();
            }
            std::uint32_t value = kTable3[index];
            if (kExtraBitsLength[index] > 0) {
                value |= bits.read(kExtraBitsLength[index]);
            }
            const std::size_t count = first4 + value + 1;

            const std::size_t distanceCode = distances.nextCode(bits);
            if (distanceCode >= std::size(kExtraBitsDistance) || distanceCode >= std::size(kBacktrackTable)) {
                corrupt();
            }
            std::uint32_t back = kBacktrackTable[distanceCode];
            if (kExtraBitsDistance[distanceCode] > 0) {
                back |= bits.read(kExtraBitsDistance[distanceCode]);
            }
            if (back >= len || len + count > expected) {
                corrupt();
            }
            // Byte by byte, because a back-reference can overlap the bytes it is writing.
            const std::size_t src = len - (back + 1);
            for (std::size_t j = 0; j < count; ++j) {
                output[len] = output[src + j];
                len += 1;
            }
        }
        progress(static_cast<double>(len) / static_cast<double>(expected));
    }
    return output;
}

}  // namespace gwl
