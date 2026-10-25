// The Guild Wars icon an account shows: the one in its Gw.exe, as a PNG data URL. Kept per file, and read again when the file changes.

#pragma once

#include <filesystem>
#include <optional>
#include <string>

namespace gwl {

// A nonblocking cache lookup for UI callers; filesystem reads belong on a worker.
std::optional<std::string> cachedGwIconUrl(const std::filesystem::path& exe);

// Reads and caches an executable icon on a worker. Missing files and icons use the caller's fallback.
std::optional<std::string> gwIconUrl(const std::filesystem::path& exe);

}  // namespace gwl
