// Starting Guild Wars, and what's running. Each platform implements these in platform/<os>/Launch.cpp.

#pragma once

#include <cstdint>
#include <filesystem>
#include <functional>
#include <optional>
#include <set>
#include <atomic>
#include <string>

#include "Progress.h"
#include <vector>

#include "Accounts.h"

namespace gwl {

// Starts the account's game. With confirm, the game waits suspended until confirm answers: true resumes it, false cancels.
// Blocks until the game's window is ready or the attempt fails. Throws std::runtime_error with the reason when it fails.
// requireSteam makes a Steam account wait for the Steam client, as the launcher does; the Windows helper passes false, because the
// Linux launcher has checked Steam already. Only Windows checks it.
void startGame(const Account& account, const std::string& dir, std::uint32_t timeoutMs, const std::function<bool()>& confirm,
               bool requireSteam = true);

// The lower-case paths, in the launcher's own form, of the Guild Wars executables that are running.
std::set<std::string> runningGamePaths();

// Game paths whose accounts are starting: from the moment their checks pass until the start ends. Safe to use from any thread.
void markStarting(const std::string& gamePath);
void unmarkStarting(const std::string& gamePath);
bool isStarting(const std::string& gamePath);

// Whether the Steam client is running.
bool steamRunning();

// Whether Steam's client is installed, and whether a Proton build is in its library.
bool steamClientInstalled();
bool protonInstalled();

// Whether this platform starts accounts through Wine: Linux does, with Steam's Proton for Steam accounts. Windows starts them directly,
// so the checks for Steam's install, Proton and Wine, and the gMod download, are Linux-only, as in the Rust launcher.
bool launchesThroughWine();

// Whether Wine runs for accounts that aren't Steam accounts: the system's, or Steam's Proton.
bool wineRuns();

// The folders a launch writes into on this platform, which must be writable for the launch to run: Linux writes its launch files to the
// launcher's current folder and the Wine prefix, and Windows writes the command-line launcher and its launch file beside the launcher.
std::vector<std::filesystem::path> launchWriteFolders(const Account& account);

// The folder a new Guild Wars install goes into, by default.
std::filesystem::path defaultGameFolder();

// Records the Gw.exe at exe as the default install, where the game looks for it. Throws std::runtime_error when it can't.
void setDefaultGame(const std::filesystem::path& exe);

// The shell command that installs Wine as root for this distro, or nothing when there's no known one.
std::optional<std::string> wineInstallCommand();

// The Steam client's folder and the Wine program the launcher finds without a manual path. Empty when there's none.
std::string detectedSteamPath();
std::string detectedWinePath();

// Sets the Steam folder and Wine program to use instead of the detected ones. Empty means the detected one.
void setInstallPaths(const std::string& steamPath, const std::string& winePath);

// Why a manual path can't be used, or nothing when both are empty or usable.
std::optional<std::string> invalidInstallPath(const std::string& steamPath, const std::string& winePath);

// Starts the Steam client in the background. Throws when it can't.
void startSteamClient();

// Whether Steam's client is ready to take requests. On Linux that's its pipe, which accepts writes only while Steam is reading it.
bool steamReady();

// Whether Steam's client reports a logged-on user, asked through the client library.
bool steamLoggedIn();
// Whether the Steam client interface, from the client library, reports a logged-on user. Missing or null is not logged in.
bool steamLoggedInOn(void* client);

// Makes Steam ready for a Steam account's launch: starts it if it isn't running, waits up to 30 seconds for its pipe, then up to 30
// seconds for a logged-on user. Reports the seconds waited, and throws kCancelledMessage when cancelled.
void startSteamForLaunch(const ProgressReport& report, const std::atomic<bool>& cancel);
// Asks Steam to install Proton Experimental. Linux only: Windows has no Proton. Throws when it can't.
void openProtonInstall();

// The folder the launcher keeps its Wine prefixes in, which is the Rust launcher's on Linux.
std::filesystem::path launcherDataDir();

// A Windows path in the form this platform reads it: a Linux path on Linux, where drive letters are mounts.
std::optional<std::string> linuxPathFor(const std::string& windowsPath);
// The file a launcher path names on this platform, or nothing when it can't be reached from here.
std::optional<std::filesystem::path> gameFilePath(const std::string& launcherPath);

}  // namespace gwl
