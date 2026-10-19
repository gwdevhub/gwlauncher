// Long work that reports its progress. Each job runs on its own thread, and its reports reach the progress window through
// runOnUiThread, so the window keeps answering while the work runs.

#pragma once

#include <atomic>
#include <functional>
#include <optional>
#include <string>

#include "Messages.h"

namespace gwl {

// The message work throws when the user cancels it, so the progress window ends quietly rather than showing a failure.
inline constexpr const char* kCancelledMessage = "cancelled";

// Reports a stage and how far through the work it is, from 0 to 1. A report with no fraction means the work has no known end, and the
// window shows it as an animation rather than a bar.
using ProgressReport = std::function<void(const std::string& stage, std::optional<double> fraction)>;

// Work that runs on a job thread. It throws std::runtime_error when it fails, or kCancelledMessage when the user cancels it.
using ProgressWork = std::function<void(const ProgressReport& report, const std::atomic<bool>& cancel)>;

// Gets the failure, or nothing when the work finished. A cancelled job's failure is kCancelledMessage.
using ProgressDone = std::function<void(const std::optional<std::string>& failure)>;

// Runs work behind a progress window that the popup describes, and calls done when it ends. The launcher's windows provide it.
using JobRunner = std::function<void(const Json& popup, const ProgressWork& work, ProgressDone done)>;

// Passes reports on at most every 100 ms, as the Rust launcher's jobs do. The reports come from one thread.
ProgressReport throttled(ProgressReport send);

}  // namespace gwl
