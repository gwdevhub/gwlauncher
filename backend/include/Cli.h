// The command-line launcher, GW_Launcher.cli.exe: `GW_Launcher.cli.exe --launch <account.json> <timeout-ms> [--confirm]`. It starts the
// game the way the Windows launcher does, then exits once the game's window is ready. With --confirm it prints "suspended" and waits
// for "resume" or "cancel" on stdin while the game is held suspended. Windows only.

#pragma once

#include <string>
#include <vector>

namespace gwl {

// Runs the command with its arguments (without the program's name), and returns the process's exit code.
int runCli(const std::vector<std::string>& arguments);

}  // namespace gwl
