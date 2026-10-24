// Starts a game in Windows CLI mode, through GW_Launcher.exe or Linux's Windows helper.

#pragma once

#include <cstdint>
#include <functional>
#include <string>

#include "Accounts.h"

namespace gwl {

// Starts the account's game and waits for its window. confirm, when given, runs while the game is suspended: true resumes it, false
// cancels. Throws std::runtime_error with the reason when the launch fails.
void launchGameInProcess(const Account& account, const std::string& dir, std::uint32_t timeoutMs, std::uint32_t windowTimeoutMs, const std::function<bool()>& confirm,
                         bool requireSteam);

}  // namespace gwl
