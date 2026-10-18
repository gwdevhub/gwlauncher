// Hashes built from the platform's primitives, for the places that compare a digest as text.

#pragma once

#include <string>

#include "utils/Strings.h"

namespace gwl {

// The SHA-256 of the bytes, as lower-case hex. Throws std::runtime_error when the platform's hash isn't available.
std::string sha256Hex(const Bytes& data);

}  // namespace gwl
