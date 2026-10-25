// A folder a launch writes into can't be written to. The launch fails part way through when it can't, so it's blocked here instead.

#include <filesystem>
#include <memory>
#include <set>
#include <string>
#include <utility>
#include <vector>

#include "Issue.h"
#include "Launch.h"
#include "ModFiles.h"
#include "utils/Files.h"

namespace gwl {

namespace {

class FolderNotWritableIssue : public Issue {
public:
    explicit FolderNotWritableIssue(std::string folder) : folder_(std::move(folder)) {}

    int severity() const override { return kBlocking; }
    std::string message() const override {
        return folder_ + " isn't writable. Check its permissions.";
    }

private:
    std::string folder_;
};

}  // namespace

std::vector<std::unique_ptr<Issue>> checkLaunchFolders(const Account& account) {
    std::vector<std::filesystem::path> folders = launchWriteFolders(account);

    // A Steam account's game folder gets its app-id file, which the game needs.
    if (account.isSteamAccount) {
        if (const auto exe = gameFilePath(account.gwpath)) {
            if (!exe->parent_path().empty()) {
                folders.push_back(exe->parent_path());
            }
        }
    }

    // Texture mods put gMod's list in gMod's folder, so that folder is written when the account has any. The list in the game's folder
    // is blanked on a best-effort basis, as in the Rust launcher, so a folder that can't take it doesn't block a launch.
    const std::filesystem::path currentDir = std::filesystem::current_path();
    const auto gmod = findGmod(launcherDataDir(), currentDir);
    if (gmod && !accountSelection(account, launcherDataDir(), gmod).texmods.empty()) {
        folders.push_back(gmod->parent_path());
    }

    std::vector<std::unique_ptr<Issue>> found;
    std::set<std::string> reported;
    for (const std::filesystem::path& folder : folders) {
        const std::string text = pathToUtf8(folder);
        if (reported.insert(text).second && !isWritableFolder(folder)) {
            found.push_back(std::make_unique<FolderNotWritableIssue>(text));
        }
    }
    return found;
}

}  // namespace gwl
