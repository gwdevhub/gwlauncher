// The Windows helper embedded in the Linux launcher, which extracts it to start games under Wine.

#pragma once

#include <cstddef>
#include <filesystem>
#include <utility>

namespace gwl {

// The embedded executable's bytes.
std::pair<const unsigned char*, std::size_t> embeddedCli();

// Copies the command-line launcher into the launcher's data folder, if the copy there isn't the same, and returns its path.
// Throws std::runtime_error when it can't be written, or when the old copy is still in use.
std::filesystem::path installEmbeddedCli();

}  // namespace gwl
