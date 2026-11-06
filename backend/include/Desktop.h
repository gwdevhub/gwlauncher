// The desktop functions the launcher defines for each platform, in platform/<os>/Desktop.cpp and Launch.cpp.

#pragma once

#include <filesystem>
#include <optional>
#include <string>
#include <utility>

namespace gwl {

// The folder WebView2 keeps the launcher's profile in. It's in LocalAppData, so it survives an update. Empty on Linux.
std::filesystem::path webViewDataFolder();

#ifdef _WIN32
// Asks whether to recreate a window whose web view failed. Retrying restarts the interface, not the jobs that are running.
bool confirmRetry(const std::string& message);
#endif

// The file the user picks in an open dialog: a Gw.exe, or a mod file when modsOnly. startFolder, when it's given, is the folder the
// dialog opens in. Nothing when they cancel.
std::optional<std::string> pickFile(const std::string& title, bool modsOnly, const std::string& startFolder = {});

// The Windows or Linux account folder the user installed Guild Wars into, when one is found.
std::optional<std::string> defaultInstall();

// The screen's size in pixels, when it can be read.
std::optional<std::pair<int, int>> screenSize();

// Writes the WebView2 loader, which the launcher carries, beside the launcher, so the windows can load it. Call it before the
// windows are initialized. It does nothing on Linux.
void installWebView2Loader();

}  // namespace gwl
