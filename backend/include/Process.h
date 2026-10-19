// Running other programs: the helpers the fixes use, such as curl and pkexec. Each platform implements these.

#pragma once

#include <string>
#include <vector>

namespace gwl {

// Runs a program and waits for it. Returns its exit code, or -1 when it couldn't be started.
int runProgram(const std::vector<std::string>& argv);

// Starts a program and doesn't wait for it.
void runProgramDetached(const std::vector<std::string>& argv);

}  // namespace gwl
