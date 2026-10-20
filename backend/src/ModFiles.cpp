#include "ModFiles.h"

#include <algorithm>
#include <cctype>
#include <system_error>

#include "Launch.h"
#include "Shell.h"
#include "ShellLink.h"
#include "utils/Files.h"
#include "utils/Strings.h"
#include "Wof.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

bool isModPath(const fs::path& path) {
    const std::string extension = lowerExtension(path);
    return extension == ".dll" || extension == ".tpf" || extension == ".zip";
}

ModType typeForPath(const fs::path& path) {
    return lowerExtension(path) == ".dll" ? ModType::Dll : ModType::Texmod;
}

// The file's absolute path when it's on this computer, which is how a shortcut's target is checked.
std::optional<fs::path> mapExisting(const fs::path& path) {
    const auto local = linuxPathFor(pathToUtf16(path));
    std::error_code error;
    if (!local || !fs::is_regular_file(pathFromUtf16(*local), error)) {
        return std::nullopt;
    }
    return fs::absolute(pathFromUtf16(*local), error);
}

// Plugin files compare by lower-case name, as the C# code orders them.
bool nameBefore(const fs::path& a, const fs::path& b) {
    return lowerCase(pathToUtf8(a.filename())) < lowerCase(pathToUtf8(b.filename()));
}

// Removes repeats, keeping the first, then sorts by file name.
std::vector<fs::path> sortedDistinct(const std::vector<fs::path>& paths) {
    std::vector<fs::path> unique;
    for (const fs::path& path : paths) {
        if (std::find(unique.begin(), unique.end(), path) == unique.end()) {
            unique.push_back(path);
        }
    }
    std::stable_sort(unique.begin(), unique.end(), nameBefore);
    return unique;
}

std::optional<PluginFile> fromShortcut(const fs::path& shortcut) {
    ShellLinkTarget link;
    try {
        link = readShellLink(shortcut);
    } catch (const std::runtime_error&) {
        // A shortcut that can't be read is kept, with no target, so the user can see it was there.
        return PluginFile{shortcut, std::nullopt, ModType::Dll};
    }
    if (!link.windowsPath) {
        return std::nullopt;
    }
    const fs::path windowsPath = pathFromUtf16(*link.windowsPath);
    if (!isModPath(windowsPath)) {
        return std::nullopt;
    }
    return PluginFile{shortcut, mapExisting(windowsPath), typeForPath(windowsPath)};
}

std::optional<PluginFile> scanPluginFile(const fs::path& path) {
    std::error_code error;
    // A file Windows compacted reads as a link to nothing on Linux, so it's a plugin only when it's compacted.
    if (!fs::is_regular_file(path, error) && !wofProblem(path)) {
        return std::nullopt;
    }
    if (lowerExtension(path) == ".lnk") {
        return fromShortcut(path);
    }
    if (!isModPath(path)) {
        return std::nullopt;
    }
    return PluginFile{path, fs::absolute(path, error), typeForPath(path)};
}

// Mod files in a folder, sorted by file name. A missing folder gives none.
std::vector<PluginFile> scanPluginFolder(const fs::path& folder) {
    std::error_code error;
    std::vector<fs::path> paths;
    for (const fs::directory_entry& entry : fs::directory_iterator(folder, error)) {
        if (entry.is_regular_file(error) || wofProblem(entry.path())) {
            paths.push_back(entry.path());
        }
    }
    std::stable_sort(paths.begin(), paths.end(), nameBefore);
    std::vector<PluginFile> files;
    for (const fs::path& path : paths) {
        if (auto file = scanPluginFile(path)) {
            files.push_back(*file);
        }
    }
    return files;
}

// The files that load: the ones whose target is here, split by kind.
ModSelection selectionOf(const std::vector<PluginFile>& files) {
    std::vector<fs::path> dlls;
    std::vector<fs::path> texmods;
    for (const PluginFile& file : files) {
        if (!file.target) {
            continue;
        }
        (file.kind == ModType::Dll ? dlls : texmods).push_back(*file.target);
    }
    return ModSelection{sortedDistinct(dlls), sortedDistinct(texmods)};
}

}  // namespace

bool ModSelection::needsGmod() const {
    return !texmods.empty() && std::none_of(dlls.begin(), dlls.end(), [](const fs::path& dll) { return isGmod(dll); });
}

ModSelection ModSelection::withGmod(const fs::path& gmod) const {
    std::vector<fs::path> withGmodDlls = dlls;
    withGmodDlls.push_back(gmod);
    return ModSelection{sortedDistinct(withGmodDlls), texmods};
}

bool modFileExists(const std::u16string& fileName) {
    const auto local = linuxPathFor(fileName);
    if (!local) {
        return false;
    }
    std::error_code error;
    const fs::path path = pathFromUtf16(*local);
    if (lowerExtension(path) == ".lnk") {
        try {
            const auto target = readShellLink(path).windowsPath;
            const auto targetLocal = target ? linuxPathFor(*target) : std::nullopt;
            return targetLocal && fs::is_regular_file(pathFromUtf16(*targetLocal), error);
        } catch (const std::runtime_error&) {
            return false;
        }
    }
    return fs::is_regular_file(path, error);
}

Json refreshedMod(Json mod) {
    const bool exists = mod.contains("fileName") && mod["fileName"].is_string() && modFileExists(utf8ToUtf16(mod["fileName"].get<std::string>()));
    mod["problem"] = exists ? "" : "File not found";
    mod["canLocate"] = false;
    mod["canResolve"] = false;
    return mod;
}

bool isGmod(const fs::path& path) {
    const std::string name = pathToUtf8(path.filename());
    return lowerCase(name) == "gmod.dll";
}

std::vector<fs::path> pluginFolders(const std::optional<fs::path>& gameFile, const fs::path& launcherDir) {
    std::vector<fs::path> folders{launcherDir / "plugins"};
    if (gameFile && !gameFile->parent_path().empty()) {
        folders.push_back(gameFile->parent_path() / "plugins");
    }
    return folders;
}

ModSelection accountSelection(const Account& account, const fs::path& launcherDir, const std::optional<fs::path>& gmod) {
    std::vector<PluginFile> files;
    if (account.usePluginFolderMods) {
        for (const fs::path& folder : pluginFolders(gameFilePath(account.gwpath), launcherDir)) {
            for (PluginFile& file : scanPluginFolder(folder)) {
                files.push_back(std::move(file));
            }
        }
    }
    for (const Mod& active : account.mods) {
        if (!active.active) {
            continue;
        }
        const auto local = gameFilePath(active.fileName);
        if (!local) {
            continue;
        }
        if (auto file = scanPluginFile(*local)) {
            files.push_back(*file);
        }
    }

    ModSelection selection = selectionOf(files);
    if (gmod && selection.needsGmod()) {
        return selection.withGmod(*gmod);
    }
    return selection;
}

std::optional<fs::path> findGmod(const fs::path& launcherDir, const fs::path& currentDir) {
    const fs::path exeDir = executablePath().parent_path();
    for (const fs::path& candidate : {launcherDir / "gMod.dll", currentDir / "gMod.dll", exeDir / "gMod.dll"}) {
        std::error_code error;
        if (fs::is_regular_file(candidate, error)) {
            return fs::absolute(candidate, error);
        }
    }
    return std::nullopt;
}

Json pluginFilesJson(const Json& draft, const fs::path& launcherDir) {
    std::optional<fs::path> gameFile;
    if (draft.contains("gwPath") && draft["gwPath"].is_string()) {
        const std::string gwPath = trimmed(draft["gwPath"].get<std::string>());
        if (!gwPath.empty()) {
            gameFile = gameFilePath(utf8ToUtf16(gwPath));
        }
    }
    Json files = Json::array();
    for (const fs::path& folder : pluginFolders(gameFile, launcherDir)) {
        for (const PluginFile& plugin : scanPluginFolder(folder)) {
            if (!plugin.target) {
                continue;
            }
            files.push_back(Json{
                {"fileName", pathToUtf8(*plugin.target)},
                {"type", static_cast<int>(plugin.kind)},
                {"active", true},
                {"readOnly", true},
                {"canLocate", false},
            });
        }
    }
    return files;
}

}  // namespace gwl
