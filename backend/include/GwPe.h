// Reads the file id a Gw.exe was built with, from the code that reports it. A port of the Rust launcher's gw_pe, which follows
// the C# GuildWarsExecutableParser for 32-bit (PE32) executables, which is what ArenaNet ships.

#pragma once

#include <cstdint>
#include <optional>
#include <vector>

#include "utils/Strings.h"

namespace gwl {

// Whether the data is a 32-bit executable at all.
bool isGwExecutable(const std::vector<std::uint8_t>& data);

// The file id, or nothing when the executable doesn't have the code that reports it.
std::optional<std::int32_t> gwFileId(const std::vector<std::uint8_t>& data);

// The executable's icon as an ICO file: its first icon group, with the images that group lists. Nothing when there's no icon.
std::optional<Bytes> gwIconFile(const std::vector<std::uint8_t>& data);

}  // namespace gwl
