#include <stdexcept>
#include <string>
#include <system_error>
#include <vector>

#include "LauncherUpdate.h"
#include "utils/Files.h"

namespace gwl {

const char* launcherAssetName() {
    return "GW_Launcher.exe";
}

void installLauncherRelease(const std::filesystem::path& current, const std::filesystem::path& staged) {
    namespace fs = std::filesystem;
    // The running exe is renamed rather than replaced, since Windows locks a running exe. The old one is kept until the swap works.
    const fs::path old = current.parent_path() / ".old.exe";
    std::error_code error;
    fs::remove(old, error);
    fs::rename(current, old, error);
    if (error) {
        throw std::runtime_error("The launcher couldn't be moved aside: " + error.message());
    }
    fs::rename(staged, current, error);
    if (error) {
        std::error_code restore;
        fs::rename(old, current, restore);
        throw std::runtime_error("The update couldn't be put in place: " + error.message());
    }
}

void removeLauncherLeftovers(const std::filesystem::path& current) {
    namespace fs = std::filesystem;
    std::error_code error;
    fs::remove(current.parent_path() / ".old.exe", error);
    fs::remove(current.parent_path() / ".new.exe", error);
}

}  // namespace gwl
