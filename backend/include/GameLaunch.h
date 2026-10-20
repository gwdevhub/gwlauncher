// What a Guild Wars client starts with: its command line, and the Steam app-id file a Steam account needs beside it. The same
// strings as the C# MulticlientPatch.CreateClientProcess and GameLaunchArguments, and a port of the Rust launcher's launch module.

#pragma once

#include <filesystem>
#include <optional>
#include <string>

#include "Accounts.h"

namespace gwl {

// The Steam app id a Steam account's game starts with.
inline constexpr const char* kSteamAppId = "29720";

// The full command line: the quoted game path, the login arguments, then the account's extra arguments. The extra arguments are
// always separated by a space, even when empty, as the C# launcher does.
std::u16string gameCommandLine(const std::u16string& gwpath, const Account& account);

// The Steam app-id file beside the game. Nothing when the game path has no folder.
std::optional<std::filesystem::path> steamAppIdPath(const std::u16string& gwpath);

// Writes the Steam app-id file for a Steam account. Does nothing for other accounts. Throws std::runtime_error when it can't be written.
void writeSteamAppId(const Account& account);

// Removes the Steam app-id file for a Steam account, if it's there. Throws std::runtime_error when it can't be removed.
void removeSteamAppId(const Account& account);

}  // namespace gwl
