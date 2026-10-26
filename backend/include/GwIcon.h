// PNG icon bytes from PE, ICO or PNG files, cached for the life of the process and refreshed when the file changes.

#pragma once

#include <filesystem>
#include <optional>
#include "utils/Strings.h"

namespace gwl {

// Reads on a worker; missing files and unsupported formats return nothing.
std::optional<Bytes> pathIcon(const std::filesystem::path& path);

}  // namespace gwl
