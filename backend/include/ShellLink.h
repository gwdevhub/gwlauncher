// Reads the target of a Windows shortcut (.lnk), per MS-SHLLINK. A port of the Rust launcher's shell_link, which follows the C#
// ShellLink class. The bytes read the same on every platform, so there's no platform code here.

#pragma once

#include <cstdint>
#include <filesystem>
#include <optional>
#include <string>
#include <vector>

namespace gwl {

struct ShellLinkTarget {
    // The path the shortcut points at, in Windows form. Nothing when the shortcut has no target.
    std::optional<std::u16string> windowsPath;
    std::optional<std::u16string> arguments;
};

// Reads the shortcut at path. Throws std::runtime_error when the file isn't a shortcut, or is cut short.
ShellLinkTarget readShellLink(const std::filesystem::path& path);

// Parses shortcut bytes. A relative target is resolved against shortcutFolder.
ShellLinkTarget parseShellLink(const std::vector<std::uint8_t>& data, const std::filesystem::path& shortcutFolder);

}  // namespace gwl
