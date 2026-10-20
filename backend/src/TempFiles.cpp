#include "TempFiles.h"

#include <filesystem>
#include <string>
#include <system_error>
#include <vector>

#include "Launch.h"
#include "utils/Files.h"

namespace gwl {

namespace fs = std::filesystem;

namespace {

// The names the launcher gives its temp files: a launch's account file and its error file, the gMod and Steam icon downloads,
// the release lists fetched to the system temp folder, and the write probe the folder check makes.
bool isLeftover(const std::string& name) {
    const std::string launch = "launch-account-";
    const bool launchFile = name.rfind(launch, 0) == 0 && (name.ends_with(".json") || name.ends_with(".json.errors"));
    const bool download = name == "gMod.dll.download" || name == "steam-favicon.ico.download";
    const bool release = name == "gwl-gmod-releases.json" || name == "gwl-launcher-releases.json";
    const bool probe = name.rfind(".gwl-write-test-", 0) == 0;
    return launchFile || download || release || probe;
}

void removeIn(const fs::path& folder) {
    std::error_code error;
    for (fs::directory_iterator entries(folder, error), end; !error && entries != end; entries.increment(error)) {
        std::error_code ignored;
        if (entries->is_regular_file(ignored) && isLeftover(pathToUtf8(entries->path().filename()))) {
            fs::remove(entries->path(), ignored);
        }
    }
}

}  // namespace

void removeLeftoverTempFiles() {
    std::vector<fs::path> folders{launcherDataDir(), fs::current_path()};
    std::error_code error;
    folders.push_back(fs::temp_directory_path(error));
    for (const fs::path& folder : folders) {
        if (!folder.empty()) {
            removeIn(folder);
        }
    }
}

}  // namespace gwl
