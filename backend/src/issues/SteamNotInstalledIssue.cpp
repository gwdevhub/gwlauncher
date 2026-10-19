// Steam's Linux client isn't installed, so a Steam account has nothing to run under. The fix opens Steam's download page.

#include "Issue.h"
#include "Launch.h"
#include "MachineFacts.h"
#include "Shell.h"

namespace gwl {

namespace {

class SteamNotInstalledIssue : public Issue {
public:
    int severity() const override { return kBlocking; }
    std::string message() const override { return "Steam for Linux isn't installed, so a Steam account can't start."; }
    void describe(Json& json) const override { json["canInstallSteam"] = true; }

    void fix(Reply reply, const JobRunner&) override {
        openUrl("https://store.steampowered.com/about/");
        reply.result("Steam's download page is open in your browser.");
    }
};

}  // namespace

std::unique_ptr<Issue> checkSteamNotInstalled(const Account& account, const MachineFacts& facts) {
    if (!launchesThroughWine() || !account.isSteamAccount || facts.steamClientInstalled) {
        return nullptr;
    }
    return std::make_unique<SteamNotInstalledIssue>();
}

}  // namespace gwl
