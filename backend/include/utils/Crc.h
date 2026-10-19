// CRC-32 with the reflected polynomial 0xEDB88320, the one Guild Wars' file server uses for its file checks. It can be continued
// from an earlier value, so data can be checked in pieces.

#pragma once

#include <cstdint>

#include "utils/Bytes.h"

namespace gwl {

// The CRC of data, continued from previous. Pass 0 to start: crc32Update(crc32Update(0, a), b) is the CRC of a followed by b.
std::uint32_t crc32Update(std::uint32_t previous, const Bytes& data);

}  // namespace gwl
