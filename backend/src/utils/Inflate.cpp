// Inflate, as RFC 1951 and RFC 1950 define it: the zlib wrapper and the deflate blocks inside it.

#include <cstddef>
#include <stdexcept>
#include <vector>

#include "utils/Bytes.h"
#include "utils/Images.h"

namespace gwl {

namespace {

[[noreturn]] void corrupt(const char* what) {
    throw std::runtime_error(std::string("Compressed data is corrupt: ") + what);
}

class BitInput {
public:
    explicit BitInput(const Bytes& data, std::size_t start) : data_(data), next_(start) {}

    std::uint32_t bits(std::uint32_t count) {
        std::uint32_t value = 0;
        for (std::uint32_t i = 0; i < count; ++i) {
            if (bitCount_ == 0) {
                if (next_ >= data_.size()) {
                    corrupt("the data ends early");
                }
                bitBuffer_ = data_[next_++];
                bitCount_ = 8;
            }
            value |= (bitBuffer_ & 1u) << i;
            bitBuffer_ >>= 1;
            bitCount_ -= 1;
        }
        return value;
    }

    // Drops the bits left in the current byte, for a stored block.
    void alignToByte() {
        bitCount_ = 0;
    }

    std::size_t nextByte() const {
        return next_;
    }

    void skipBytes(std::size_t count) {
        next_ += count;
    }

    const Bytes& data() const {
        return data_;
    }

private:
    const Bytes& data_;
    std::size_t next_;
    std::uint32_t bitBuffer_ = 0;
    std::uint32_t bitCount_ = 0;
};

// A canonical Huffman code: how many codes have each length, and the symbols in code order.
struct Code {
    std::vector<std::uint16_t> count;
    std::vector<std::uint16_t> symbols;
};

Code buildCode(const std::vector<std::uint16_t>& lengths) {
    Code code;
    code.count.assign(16, 0);
    for (std::uint16_t length : lengths) {
        code.count[length] += 1;
    }
    std::vector<std::uint16_t> offsets(16, 0);
    for (int length = 1; length < 15; ++length) {
        offsets[length + 1] = static_cast<std::uint16_t>(offsets[length] + code.count[length]);
    }
    code.symbols.assign(lengths.size(), 0);
    for (std::size_t symbol = 0; symbol < lengths.size(); ++symbol) {
        if (lengths[symbol] != 0) {
            code.symbols[offsets[lengths[symbol]]++] = static_cast<std::uint16_t>(symbol);
        }
    }
    return code;
}

std::uint16_t decodeSymbol(BitInput& input, const Code& code) {
    int first = 0;
    int index = 0;
    int value = 0;
    for (int length = 1; length < 16; ++length) {
        value |= static_cast<int>(input.bits(1));
        const int count = code.count[length];
        if (value - count < first) {
            return code.symbols[static_cast<std::size_t>(index + (value - first))];
        }
        index += count;
        first += count;
        first <<= 1;
        value <<= 1;
    }
    corrupt("an unknown code");
}

const std::uint16_t kLengthBase[29] = {3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258};
const std::uint16_t kLengthExtra[29] = {0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0};
const std::uint16_t kDistanceBase[30] = {1,   2,   3,   4,   5,   7,    9,    13,   17,   25,   33,   49,   65,   97,    129,
                                         193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577};
const std::uint16_t kDistanceExtra[30] = {0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13};

// Decodes the literal and length codes of one block, into out.
void inflateCodes(BitInput& input, Bytes& out, const Code& literals, const Code& distances) {
    for (;;) {
        const std::uint16_t symbol = decodeSymbol(input, literals);
        if (symbol < 256) {
            out.push_back(static_cast<std::uint8_t>(symbol));
            continue;
        }
        if (symbol == 256) {
            return;
        }
        const std::size_t lengthIndex = symbol - 257;
        if (lengthIndex >= 29) {
            corrupt("a length code is out of range");
        }
        const std::size_t length = kLengthBase[lengthIndex] + input.bits(kLengthExtra[lengthIndex]);
        const std::uint16_t distanceSymbol = decodeSymbol(input, distances);
        if (distanceSymbol >= 30) {
            corrupt("a distance code is out of range");
        }
        const std::size_t distance = kDistanceBase[distanceSymbol] + input.bits(kDistanceExtra[distanceSymbol]);
        if (distance > out.size()) {
            corrupt("a distance reaches before the start");
        }
        const std::size_t source = out.size() - distance;
        for (std::size_t i = 0; i < length; ++i) {
            out.push_back(out[source + i]);
        }
    }
}

void readDynamicCodes(BitInput& input, Code& literals, Code& distances) {
    static const std::uint16_t kOrder[19] = {16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15};
    const std::size_t literalCount = input.bits(5) + 257;
    const std::size_t distanceCount = input.bits(5) + 1;
    const std::size_t codeLengthCount = input.bits(4) + 4;

    std::vector<std::uint16_t> codeLengthLengths(19, 0);
    for (std::size_t i = 0; i < codeLengthCount; ++i) {
        codeLengthLengths[kOrder[i]] = static_cast<std::uint16_t>(input.bits(3));
    }
    const Code codeLengthCode = buildCode(codeLengthLengths);

    std::vector<std::uint16_t> lengths;
    while (lengths.size() < literalCount + distanceCount) {
        const std::uint16_t symbol = decodeSymbol(input, codeLengthCode);
        if (symbol < 16) {
            lengths.push_back(symbol);
        } else if (symbol == 16) {
            if (lengths.empty()) {
                corrupt("a repeat with nothing to repeat");
            }
            const std::uint16_t previous = lengths.back();
            const std::size_t repeat = 3 + input.bits(2);
            lengths.insert(lengths.end(), repeat, previous);
        } else if (symbol == 17) {
            lengths.insert(lengths.end(), 3 + input.bits(3), 0);
        } else {
            lengths.insert(lengths.end(), 11 + input.bits(7), 0);
        }
    }
    if (lengths.size() != literalCount + distanceCount) {
        corrupt("the code lengths overrun their table");
    }
    literals = buildCode(std::vector<std::uint16_t>(lengths.begin(), lengths.begin() + static_cast<std::ptrdiff_t>(literalCount)));
    distances = buildCode(std::vector<std::uint16_t>(lengths.begin() + static_cast<std::ptrdiff_t>(literalCount), lengths.end()));
}

}  // namespace

// The deflate blocks that start at start. They run to the last block, and anything after them is left alone.
static Bytes inflateBlocks(const Bytes& input, std::size_t start) {
    BitInput bits(input, start);
    Bytes out;
    bool last = false;
    while (!last) {
        last = bits.bits(1) == 1;
        const std::uint32_t type = bits.bits(2);
        if (type == 0) {
            bits.alignToByte();
            const std::size_t at = bits.nextByte();
            if (input.size() < at + 4) {
                corrupt("a stored block header is cut off");
            }
            const std::size_t length = input[at] | input[at + 1] << 8;
            const std::size_t complement = input[at + 2] | input[at + 3] << 8;
            if ((length ^ 0xFFFF) != complement) {
                corrupt("a stored block length doesn't match its complement");
            }
            if (input.size() < at + 4 + length) {
                corrupt("a stored block is cut off");
            }
            out.insert(out.end(), input.begin() + static_cast<std::ptrdiff_t>(at + 4),
                       input.begin() + static_cast<std::ptrdiff_t>(at + 4 + length));
            bits.skipBytes(4 + length);
        } else if (type == 1) {
            std::vector<std::uint16_t> lengths(288, 8);
            for (std::size_t i = 144; i < 256; ++i) {
                lengths[i] = 9;
            }
            for (std::size_t i = 256; i < 280; ++i) {
                lengths[i] = 7;
            }
            const Code literals = buildCode(lengths);
            const Code distances = buildCode(std::vector<std::uint16_t>(30, 5));
            inflateCodes(bits, out, literals, distances);
        } else if (type == 2) {
            Code literals;
            Code distances;
            readDynamicCodes(bits, literals, distances);
            inflateCodes(bits, out, literals, distances);
        } else {
            corrupt("an unknown block type");
        }
    }
    return out;
}

Bytes zlibInflate(const Bytes& input) {
    if (input.size() < 2 || (input[0] & 0x0F) != 8) {
        corrupt("not a zlib stream");
    }
    return inflateBlocks(input, 2);
}

Bytes gzipInflate(const Bytes& input) {
    if (input.size() < 18 || input[0] != 0x1f || input[1] != 0x8b || input[2] != 8) {
        corrupt("not a gzip stream");
    }
    const std::uint8_t flags = input[3];
    std::size_t pos = 10;
    if (flags & 4) {
        // FEXTRA: a length, then that many bytes.
        if (input.size() < pos + 2) {
            corrupt("the gzip header is cut off");
        }
        pos += 2 + (input[pos] | input[pos + 1] << 8);
    }
    // FNAME and FCOMMENT are zero-terminated strings.
    for (const std::uint8_t flag : {std::uint8_t{8}, std::uint8_t{16}}) {
        if (flags & flag) {
            while (pos < input.size() && input[pos] != 0) {
                pos += 1;
            }
            pos += 1;
        }
    }
    if (flags & 2) {
        pos += 2;
    }
    if (pos + 8 > input.size()) {
        corrupt("the gzip data is cut off");
    }
    Bytes out = inflateBlocks(input, pos);
    // The trailer holds the uncompressed size, modulo 2^32, which must match.
    if (readU32LE(input, input.size() - 4) != static_cast<std::uint32_t>(out.size())) {
        corrupt("the gzip size doesn't match its data");
    }
    return out;
}

}  // namespace gwl
