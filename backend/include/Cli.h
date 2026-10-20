// Windows game-launch mode, used by GW_Launcher.exe and Linux's GW_Launcher.cli.exe helper.

#pragma once

#include <string>
#include <vector>

namespace gwl {

// Reads the native Windows command line as UTF-8, including the program's name.
std::vector<std::string> commandLineArguments();

// Runs the command with its arguments (without the program's name), and returns the process's exit code.
int runCli(const std::vector<std::string>& arguments);

}  // namespace gwl
