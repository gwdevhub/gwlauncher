// The command-line launcher, GW_Launcher.cli.exe, which the GUI launchers carry inside themselves. The GUI copies it out when it needs it
// and runs it to start a game. The bytes are linked in by the GUI build only.

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
