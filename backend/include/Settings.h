#pragma once

#include <cstdint>
#include <optional>
#include <string>

#include "Messages.h"

namespace gwl {

// Settings.json, with the fields and names of the C# GlobalSettings class.
struct Settings {
    // Always on: the launcher checks for updates at every start. Kept in the file for compatibility.
    bool checkForUpdates = true;
    bool promptForUpdates = true;
    bool launchMinimized = false;
    bool keepInSystemTray = true;
    std::int64_t listWidth = 220;
    std::int64_t listHeight = 340;
    std::optional<std::int64_t> listX;
    std::optional<std::int64_t> listY;
    std::uint32_t timeoutOnModlaunch = 10000;
    // The Steam client's folder and the Wine program, when set by hand. Empty means the launcher finds them.
    std::string steamPath;
    std::string winePath;
    // The page's size, in percent of the normal size. Between 50 and 200.
    std::int64_t uiScale = 100;

    // The settings the launcher runs with on this computer: the tray options are off where there's no tray.
    Settings effective(bool hasTray) const;

    // Writes the settings to the file.
    void save(const std::string& path) const;

    // These settings with the page's edits applied. The timeout must be a whole number of milliseconds above zero.
    Settings applyFields(const Json& fields) const;

    // The settings as the page sees them.
    Json forPage() const;
};

// Reads the file. A missing file gives the defaults. Throws std::runtime_error when the file isn't valid.
Settings loadSettings(const std::string& path);

// Writes the file as the C# Save does: indented, the same key order, CRLF line endings.



}  // namespace gwl
