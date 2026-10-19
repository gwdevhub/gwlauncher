#include "utils/Bytes.h"

namespace gwl {

std::uint16_t readU16LE(const Bytes& data, std::size_t offset) {
    return static_cast<std::uint16_t>(data[offset] | data[offset + 1] << 8);
}

std::uint32_t readU32LE(const Bytes& data, std::size_t offset) {
    return static_cast<std::uint32_t>(data[offset]) | static_cast<std::uint32_t>(data[offset + 1]) << 8 |
           static_cast<std::uint32_t>(data[offset + 2]) << 16 | static_cast<std::uint32_t>(data[offset + 3]) << 24;
}

void appendU16LE(Bytes& data, std::uint16_t value) {
    data.push_back(static_cast<std::uint8_t>(value));
    data.push_back(static_cast<std::uint8_t>(value >> 8));
}

void appendU32LE(Bytes& data, std::uint32_t value) {
    for (int i = 0; i < 4; ++i) {
        data.push_back(static_cast<std::uint8_t>(value >> (8 * i)));
    }
}

}  // namespace gwl
