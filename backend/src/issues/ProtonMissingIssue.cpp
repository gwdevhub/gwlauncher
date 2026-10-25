// Steam's library has no Proton, so a Steam account can't run. The fix opens Steam's page to install Proton Experimental.

#include <stdexcept>

#include "Issue.h"
#include "Launch.h"
#include "MachineFacts.h"

namespace gwl {

namespace {

class ProtonMissingIssue : public Issue {
public:
    int severity() const override { return kBlocking; }
    std::string message() const override { return "Proton isn't installed in Steam's library."; }

    void describe(Json& json) const override { json["canInstallProton"] = true; }

    void fix(Reply reply, const JobRunner& runJob) override {
        try {
            openProtonInstall();
            reply.result("Confirm the Proton Experimental installation in Steam.");
        } catch (const std::exception&) {
            reply.result(nullptr);
        }
    }
};

}  // namespace

std::unique_ptr<Issue> checkProtonMissing(const Account& account, const MachineFacts& facts) {
    if (!launchesThroughWine() || !account.isSteamAccount || !facts.steamClientInstalled || facts.protonInstalled) {
        return nullptr;
    }
    return std::make_unique<ProtonMissingIssue>();
}

}  // namespace gwl
