// An active mod's file isn't where the account says. It's a warning: the account still launches without it.

#include <filesystem>

#include "Issue.h"
#include "Launch.h"
#include "ModFiles.h"
#include "Wof.h"
#include "Launch.h"

namespace gwl {

namespace {

class ModMissingIssue : public Issue {
public:
    explicit ModMissingIssue(std::string fileName) : name_(std::filesystem::path(fileName).filename().string()) {}

    int severity() const override { return kWarning; }
    std::string message() const override { return name_ + " was not found."; }

private:
    std::string name_;
};

}  // namespace

std::vector<std::unique_ptr<Issue>> checkModsMissing(const Account& account) {
    std::vector<std::unique_ptr<Issue>> missing;
    for (const Mod& mod : account.mods) {
        if (!mod.active) {
            continue;
        }
        // A compacted file is there, and checkWofProblems reports it instead. A shortcut counts when its target does.
        const auto modFile = gameFilePath(mod.fileName);
        if (modFile && wofProblem(*modFile)) {
            continue;
        }
        if (!modFileExists(mod.fileName)) {
            missing.push_back(std::make_unique<ModMissingIssue>(mod.fileName));
        }
    }
    return missing;
}

}  // namespace gwl
