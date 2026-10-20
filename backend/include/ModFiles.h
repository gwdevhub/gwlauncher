// The mod files an account loads: whether they're on this computer, the plugin folders' mods, and the DLLs and texture mods one launch
// loads. A port of the Rust launcher's mods (PluginScanner, ModSelection) and of the mod checks in its launch bridge.

#pragma once

#include <filesystem>
#include <optional>
#include <string>
#include <vector>

#include "Accounts.h"
#include "Messages.h"

namespace gwl {

// A mod file found in a plugin folder, or named by an account. target is the file that loads, or nothing when a shortcut's target
// isn't on this computer.
struct PluginFile {
    std::filesystem::path source;
    std::optional<std::filesystem::path> target;
    ModType kind = ModType::Dll;
};

// The DLLs and texture mods one launch loads, each sorted by file name. Files whose target isn't here are left out.
struct ModSelection {
    std::vector<std::filesystem::path> dlls;
    std::vector<std::filesystem::path> texmods;

    // Texture mods run through gMod, so gMod is needed when there are texture mods and no gMod among the DLLs.
    bool needsGmod() const;
    ModSelection withGmod(const std::filesystem::path& gmod) const;
};

// Whether the mod's file exists. A shortcut counts when the file it points at does.
bool modFileExists(const std::u16string& fileName);

// The mod draft, with the problem and the actions it offers checked against this computer. A missing file is reported as a problem.
// Nothing here can locate or resolve a file yet, so both actions are off, as in the Rust launcher.
Json refreshedMod(Json mod);

// Whether the file is gMod.dll, by name.
bool isGmod(const std::filesystem::path& path);

// The plugin folders for a game at gameFile: the launcher's own, then the one beside the game.
std::vector<std::filesystem::path> pluginFolders(const std::optional<std::filesystem::path>& gameFile,
                                                 const std::filesystem::path& launcherDir);

// The DLLs and texture mods one launch of the account loads. gMod is added when texture mods need it.
ModSelection accountSelection(const Account& account, const std::filesystem::path& launcherDir,
                              const std::optional<std::filesystem::path>& gmod);

// gMod.dll beside the launcher, in the current folder, or beside the launcher's executable, as the C# FindGMod looks for it.
std::optional<std::filesystem::path> findGmod(const std::filesystem::path& launcherDir, const std::filesystem::path& currentDir);

// The "pluginFiles" answer for the account draft: the mods of its plugin folders, with their targets, read-only.
Json pluginFilesJson(const Json& draft, const std::filesystem::path& launcherDir);

}  // namespace gwl
