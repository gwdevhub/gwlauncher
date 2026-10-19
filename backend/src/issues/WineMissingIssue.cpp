// No Wine runs for an account that isn't a Steam account. The fix installs the distro's Wine through pkexec, in the
// background, and the reply says so straight away.

#include <optional>
#include <string>
#include <thread>

#include "Issue.h"
#include "Launch.h"
#include "MachineFacts.h"
#include "Process.h"
#include "WebViewWindow.h"

namespace gwl {

namespace {

class WineMissingIssue : public Issue {
public:
    explicit WineMissingIssue(std::optional<std::string> installCommand) : installCommand_(std::move(installCommand)) {}

    int severity() const override { return kBlocking; }
    std::string message() const override { return "Wine isn't installed, so this account can't start."; }

    void describe(Json& json) const override { json["canInstallWine"] = installCommand_.has_value(); }

    void fix(Reply reply, const JobRunner& runJob) override {
        if (!installCommand_) {
            reply.result(nullptr);
            return;
        }
        reply.result("Wine is installing. Launch the account once it's done.");
        std::thread([command = *installCommand_] {
            const int status = runProgram({"pkexec", "sh", "-c", command});
            if (status == 0) {
                return;
            }
            // -1 means pkexec couldn't be started at all, so the cause is its absence, not the install.
            const std::string message = status < 0
                ? "pkexec isn't installed, so Wine can't be installed from here. Install polkit, which provides pkexec, or install Wine yourself with: " + command
                : "Wine couldn't be installed. Install it yourself with: " + command;
            runOnUiThread([message] { showError(message); });
        }).detach();
    }

private:
    std::optional<std::string> installCommand_;
};

}  // namespace

std::unique_ptr<Issue> checkWineMissing(const Account& account, const MachineFacts& facts) {
    if (account.isSteamAccount || !launchesThroughWine() || facts.wineRuns) {
        return nullptr;
    }
    return std::make_unique<WineMissingIssue>(facts.wineInstall);
}

}  // namespace gwl
