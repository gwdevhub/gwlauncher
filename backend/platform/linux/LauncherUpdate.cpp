#include <stdexcept>
#include <string>
#include <system_error>
#include <vector>

#include "LauncherUpdate.h"
#include "Process.h"

namespace gwl {

namespace {

// The update is kept beside the running launcher under this name until the next start removes it.
std::string oldName(const std::filesystem::path& current) {
    return ".old-" + current.filename().string();
}

}  // namespace

// The release's own binary, GW_Launcher, which the launcher downloads and puts in place of itself.
const char* launcherAssetName() {
    return "GW_Launcher";
}

void installLauncherRelease(const std::filesystem::path& current, const std::filesystem::path& staged,
                            const std::vector<std::string>& arguments) {
    namespace fs = std::filesystem;
    const fs::path dir = current.parent_path();
    const fs::path old = dir / oldName(current);
    std::error_code error;
    fs::permissions(staged,
                    fs::perms::owner_all | fs::perms::group_read | fs::perms::group_exec | fs::perms::others_read | fs::perms::others_exec,
                    fs::perm_options::replace, error);
    if (error) {
        fs::remove(staged, error);
        throw std::runtime_error("The update couldn't be made runnable: " + error.message());
    }

    // The running launcher's file is moved aside rather than overwritten, so a failed swap can be undone.
    fs::remove(old, error);
    fs::rename(current, old, error);
    if (error) {
        fs::remove(staged, error);
        throw std::runtime_error("The launcher couldn't be moved aside for the update: " + error.message());
    }
    fs::rename(staged, current, error);
    if (error) {
        std::error_code restore;
        fs::rename(old, current, restore);
        fs::remove(staged, restore);
        throw std::runtime_error("The update couldn't be put in place: " + error.message());
    }

    std::vector<std::string> command{current.string()};
    command.insert(command.end(), arguments.begin(), arguments.end());
    runProgramDetached(command);
}

void removeLauncherLeftovers(const std::filesystem::path& current) {
    namespace fs = std::filesystem;
    const fs::path dir = current.parent_path();
    std::error_code error;
    fs::remove(dir / oldName(current), error);
    fs::remove(dir / ".new", error);
}

}  // namespace gwl
