// Shell services: the clipboard, opening links and files in the file manager, the desktop folder, and desktop shortcuts.
// Each platform implements these in platform/<os>/Shell.cpp.

#pragma once

#include <filesystem>
#include <optional>
#include <string>

namespace gwl {

// Puts text on the clipboard. The launcher keeps it there while it runs.
void copyText(const std::string& text);

// Opens a link in the browser.
void openUrl(const std::string& url);

// Shows the file in the file manager. Linux file managers can't select a file portably, so its folder opens instead.
void revealInFileExplorer(const std::filesystem::path& path);

// The user's desktop folder, when it can be found.
std::optional<std::filesystem::path> desktopFolder();

// The launcher's own executable.
std::filesystem::path executablePath();

// Writes a desktop shortcut at file that starts target with arguments. iconPng is the PNG the shortcut shows, where the
// platform uses one. Throws std::runtime_error with the reason when it fails.
void createShortcut(const std::filesystem::path& file, const std::filesystem::path& target, const std::string& arguments,
                    const std::string& description, const std::string& iconPng);

}  // namespace gwl
