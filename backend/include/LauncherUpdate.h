// GW Launcher's own updates: finding the newest release, downloading it, and putting it in place of the running launcher. A port of
// the Rust launcher's launcher_update, which follows the C# self-update. Releases are tagged r<revision>.0, where the revision is the
// commit count the release was built from.

#pragma once

#include <atomic>
#include <cstdint>
#include <filesystem>
#include <optional>
#include <string>
#include <vector>

#include "Progress.h"

namespace gwl {

struct LauncherRelease {
    std::uint32_t revision = 0;
    std::string notes;
    std::string url;
    std::string download;
    std::string sha256;
};

// The revision a tag names, so r600.0 is 600. Nothing for a tag without one.
std::optional<std::uint32_t> revisionOfTag(const std::string& tag);

// The newest published release that has this platform's asset, from GitHub's releases JSON. Nothing when there isn't one.
// Throws std::runtime_error when the JSON isn't a release list, or the asset has no checksum to check the download against.
std::optional<LauncherRelease> releaseFromJson(const std::string& releasesJson);

// The newest release, from GitHub. GWLAUNCHER_RELEASES_URL points it at another copy of the releases JSON, for testing the flow.
// Throws std::runtime_error when GitHub can't be reached.
std::optional<LauncherRelease> latestLauncherRelease();

// The commit count this build was made from. Zero when it isn't known, and then no update is checked for.
std::uint32_t currentBuild();

// Downloads the release to dest and checks it against its checksum, so a bad download never replaces the launcher. Throws
// std::runtime_error with the reason, or kCancelledMessage when cancelled.
void downloadLauncherRelease(const LauncherRelease& release, const std::filesystem::path& dest, const std::atomic<bool>& cancel,
                             const ProgressReport& report);

// The release asset the launcher downloads to update itself: GW_Launcher.exe on Windows, GW_Launcher on Linux.
const char* launcherAssetName();

// Puts the downloaded release in place of the running launcher, and starts the new one with the same arguments. The old files are
// kept until the swap has worked. Throws std::runtime_error with the reason when it fails. Implemented in platform/<os>/LauncherUpdate.cpp.
void installLauncherRelease(const std::filesystem::path& current, const std::filesystem::path& staged, const std::vector<std::string>& arguments);

// Deletes what a previous update left beside the running launcher. Implemented in platform/<os>/LauncherUpdate.cpp.
void removeLauncherLeftovers(const std::filesystem::path& current);

}  // namespace gwl
