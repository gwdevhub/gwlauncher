// The Guild Wars icon an account shows: the one in its Gw.exe, as a PNG data URL. Kept per file, and read again when the file changes.

#pragma once

#include <filesystem>
#include <optional>
#include <string>

namespace gwl {

// The icon of the Gw.exe at exe, scaled for an account row. Nothing when the file is missing or has no icon. Keeps its results
// for the launcher's window, which is the only caller.
std::optional<std::string> gwIconUrl(const std::filesystem::path& exe);

}  // namespace gwl
