// Little-endian reads and writes, for the binary formats the launcher reads and writes: Gw.exe, WOF data and the network protocol.

#pragma once

#include <cstddef>
#include <cstdint>

#include "utils/Strings.h"

namespace gwl {

// The value at offset. The caller checks that the bytes are there.
std::uint16_t readU16LE(const Bytes& data, std::size_t offset);
std::uint32_t readU32LE(const Bytes& data, std::size_t offset);

void appendU16LE(Bytes& data, std::uint16_t value);
void appendU32LE(Bytes& data, std::uint32_t value);

}  // namespace gwl
