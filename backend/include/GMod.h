// gMod.dll: the texmod loader the launcher downloads from gwdevhub/gMod when an account needs it.

#pragma once

#include <atomic>
#include <string>

#include "Progress.h"

namespace gwl {

// Whether the file is a 32-bit Windows DLL: an MZ header, then a PE header for i386.
bool isUsableDll(const std::string& path);

// Downloads the newest gMod.dll into folder, checked against its published SHA-256 digest, and reports each step. Returns its path. Throws
// std::runtime_error with the reason when it can't be downloaded or doesn't check out, or kCancelledMessage when cancelled between steps.
// The download itself runs as one step, so it can't be cancelled part way through.
std::string downloadGMod(const std::string& folder, const ProgressReport& report, const std::atomic<bool>& cancel);

}  // namespace gwl
