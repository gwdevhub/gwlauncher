#include "utils/Crc.h"

#include <array>

namespace gwl {

namespace {

std::array<std::uint32_t, 256> makeTable() {
    std::array<std::uint32_t, 256> table{};
    for (std::uint32_t i = 0; i < table.size(); ++i) {
        std::uint32_t value = i;
        for (int bit = 0; bit < 8; ++bit) {
            value = (value & 1) ? (value >> 1) ^ 0xEDB88320u : value >> 1;
        }
        table[i] = value;
    }
    return table;
}

const std::array<std::uint32_t, 256> kTable = makeTable();

}  // namespace

std::uint32_t crc32Update(std::uint32_t previous, const Bytes& data) {
    std::uint32_t crc = ~previous;
    for (const std::uint8_t byte : data) {
        crc = kTable[(crc ^ byte) & 0xFF] ^ (crc >> 8);
    }
    return ~crc;
}

}  // namespace gwl
