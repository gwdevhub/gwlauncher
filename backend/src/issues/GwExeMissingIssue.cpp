// Gw.exe isn't at the account's path. Nothing fixes this, so the account has to be edited.

#include <filesystem>

#include "Issue.h"
#include "Launch.h"
#include "Wof.h"
#include "utils/Strings.h"

namespace gwl {

namespace {

class GwExeMissingIssue : public Issue {
public:
    explicit GwExeMissingIssue(const std::u16string& gwpath) : gwpath_(utf16ToUtf8(gwpath)) {}

    int severity() const override { return kBlocking; }
    std::string message() const override { return "Gw.exe not found at " + gwpath_ + "."; }

private:
    std::string gwpath_;
};

}  // namespace

std::unique_ptr<Issue> checkGwExeMissing(const Account& account) {
    const auto gwExe = gameFilePath(account.gwpath);
    // A compacted file is there, and checkWofProblems reports it instead.
    if (gwExe && (std::filesystem::is_regular_file(*gwExe) || wofProblem(*gwExe))) {
        return nullptr;
    }
    return std::make_unique<GwExeMissingIssue>(account.gwpath);
}

}  // namespace gwl
