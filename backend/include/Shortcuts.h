#pragma once

#include <filesystem>
#include <optional>
#include <string>

namespace gwl {

struct Account;

struct ShortcutTarget {
    std::filesystem::path file;
    std::string arguments;
    std::optional<std::filesystem::path> icon;
    int iconIndex = 0;
};

bool isShortcut(const std::filesystem::path& path);
// Follows shortcut chains and rejects cycles/malformed entries. Plain files pass through.
ShortcutTarget resolveShortcut(const std::filesystem::path& path, bool resolveIcon = true);
// Status rendering uses the last worker-resolved target without file I/O.
std::optional<std::filesystem::path> cachedShortcutTarget(const std::filesystem::path& path);
Account resolveAccountShortcut(const Account& account);

}  // namespace gwl
