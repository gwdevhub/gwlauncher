// A problem with an account on this computer. Each kind of problem is a class in its own file under src/issues. The
// IssueChecker runs their checks for an account.

#pragma once

#include <memory>
#include <string>
#include <vector>

#include "Accounts.h"
#include "MachineFacts.h"
#include "Messages.h"
#include "Progress.h"

namespace gwl {

class Issue {
public:
    // Blocking issues stop a launch. Warnings don't.
    static constexpr int kBlocking = 0;
    static constexpr int kWarning = 1;

    virtual ~Issue() = default;

    virtual int severity() const = 0;

    // What's wrong, as the issue list shows it.
    virtual std::string message() const = 0;

    // The flags the page uses for the issue's fix button. An issue with a fix overrides this to set its own flag.
    virtual void describe(Json& json) const { (void)json; }

    // Runs the issue's fix. The reply is the message to show when something changed, or null when nothing did. A fix that
    // takes a while runs through runJob, which shows its progress.
    virtual void fix(Reply reply, const JobRunner& runJob) {
        (void)runJob;
        reply.result(nullptr);
    }

    // The issue as the page holds it. Its fix is found again from this, so it must not change between checks.
    Json toJson() const;
};

// One check per kind of issue. Each returns the issue when the account has it, and nothing otherwise. The checks that
// read the installed software take the MachineFacts, so they don't measure the machine again.
std::unique_ptr<Issue> checkGwExeMissing(const Account& account);
std::unique_ptr<Issue> checkSteamNotInstalled(const Account& account, const MachineFacts& facts);
std::unique_ptr<Issue> checkProtonMissing(const Account& account, const MachineFacts& facts);
std::unique_ptr<Issue> checkWineMissing(const Account& account, const MachineFacts& facts);
std::unique_ptr<Issue> checkGModMissing(const Account& account);
std::vector<std::unique_ptr<Issue>> checkModsMissing(const Account& account);
std::vector<std::unique_ptr<Issue>> checkModsInvalid(const Account& account);
std::vector<std::unique_ptr<Issue>> checkTexmodsInvalid(const Account& account);
// A file Windows compacted, which Linux can't read. The account's Gw.exe and its active mods are checked.
std::vector<std::unique_ptr<Issue>> checkWofProblems(const Account& account);
// The folders a launch writes into can be written to. A folder that can't be is a blocking issue.
std::vector<std::unique_ptr<Issue>> checkLaunchFolders(const Account& account);
// The Gw.exe is older than the latest build. Needs the network, and an account whose check can't reach it has no issue.
std::unique_ptr<Issue> checkGwExeOutdated(const Account& account);
// The account's game is already running, so it can't be launched again.
std::unique_ptr<Issue> checkInUse(const Account& account);

}  // namespace gwl
