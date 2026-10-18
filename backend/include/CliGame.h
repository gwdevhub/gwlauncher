// Starting a game in this process, for the command-line launcher, which the Windows and Linux launchers run as GW_Launcher.cli.exe. The
// GUI doesn't call it: it starts the game through the command line. Windows only.

#pragma once

#include <cstdint>
#include <functional>
#include <string>

#include "Accounts.h"

namespace gwl {

// Starts the account's game and waits for its window. confirm, when given, runs while the game is suspended: true resumes it, false
// cancels. Throws std::runtime_error with the reason when the launch fails.
void launchGameInProcess(const Account& account, const std::string& dir, std::uint32_t timeoutMs, const std::function<bool()>& confirm,
                         bool requireSteam);

}  // namespace gwl
