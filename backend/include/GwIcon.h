// PNG icon bytes from image/PE files or shortcuts, cached per image for the process lifetime.

#pragma once

#include <filesystem>
#include <optional>
#include "utils/Strings.h"

namespace gwl {

// Reads on a worker; missing files and unsupported formats return nothing.
std::optional<Bytes> pathIcon(const std::filesystem::path& path);

}  // namespace gwl
