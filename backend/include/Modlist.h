// The modlist.txt files gMod reads texture mods from. They're written before the game resumes and put back when the guard goes out of
// scope, so a launch never leaves its own list behind. A port of the modlist part of the Rust launcher's modlist module.

#pragma once

#include <filesystem>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

namespace gwl {

class ModlistGuard {
public:
    // Nothing when the launch needs no texture list: no texture mods, or no gMod.dll to read it. Throws std::runtime_error when the
    // list can't be written.
    static std::unique_ptr<ModlistGuard> prepare(const std::vector<std::filesystem::path>& texmods,
                                                 const std::optional<std::filesystem::path>& gmodDll,
                                                 const std::filesystem::path& gameExe);

    // Puts the lists back as they were.
    ~ModlistGuard();

    ModlistGuard(const ModlistGuard&) = delete;
    ModlistGuard& operator=(const ModlistGuard&) = delete;

private:
    ModlistGuard(std::filesystem::path gmodFile, std::optional<std::string> gmodOriginal,
                 std::optional<std::pair<std::filesystem::path, std::string>> gameFile);

    std::filesystem::path gmodFile_;
    // gMod's own list before the launch, or nothing when there was none.
    std::optional<std::string> gmodOriginal_;
    // The game folder's list, kept only when it was blanked. Left alone when it's the same file as gMod's.
    std::optional<std::pair<std::filesystem::path, std::string>> gameFile_;
};

}  // namespace gwl
