// Installs and updates Gw.exe from ArenaNet. A port of the Rust launcher's gw_install, which follows the C# IntegratedGuildwarsInstaller
// and GwDownloader: a Gw.exe already in the folder is kept when its CRC matches the one ArenaNet sends for the latest build.

#pragma once

#include <atomic>
#include <filesystem>
#include <functional>
#include <string>
#include <vector>

#include "GwNet.h"
#include "Progress.h"

namespace gwl {

// Reports a stage and how far through the work it is, from 0 to 1.
using GwReport = ProgressReport;

// Makes sure the folder holds the latest Gw.exe, and returns its path. Throws std::runtime_error with the reason when it fails.
std::filesystem::path installGw(const std::filesystem::path& folder, const GwReport& report, const std::atomic<bool>& cancel);

// Whether the Gw.exe at path is out of date: its CRC, continued over the latest file id, must match the one ArenaNet sends. A
// missing file, or one that isn't a program, isn't: the check fails open.
bool gwIsOutdated(const std::filesystem::path& path, const GwFileInfo& latest);

// Replaces the Gw.exe at each path that isn't the latest build. Nothing is downloaded when they all are. The download is made once,
// however many paths there are. Returns how many paths were replaced.
std::size_t updateGwClients(const std::vector<std::filesystem::path>& paths, const GwReport& report, const std::atomic<bool>& cancel);

}  // namespace gwl
