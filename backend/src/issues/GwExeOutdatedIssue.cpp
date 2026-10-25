// The Gw.exe is older than the latest game build. The fix downloads the latest build over it, behind a progress window.

#include <atomic>
#include <exception>
#include <filesystem>
#include <memory>
#include <optional>
#include <string>

#include "GwInstall.h"
#include "GwNet.h"
#include "Issue.h"
#include "Launch.h"
#include "Progress.h"

namespace gwl {

namespace {

class GwExeOutdatedIssue : public Issue {
public:
    GwExeOutdatedIssue(std::string gwpath, std::filesystem::path exe) : gwpath_(std::move(gwpath)), exe_(std::move(exe)) {}

    int severity() const override { return kWarning; }
    std::string message() const override { return "Gw.exe is out of date."; }
    void describe(Json& json) const override { json["canUpdateGwExe"] = true; }

    void fix(Reply reply, const JobRunner& runJob) override {
        const Json popup{
            {"title", "Updating Gw.exe"},
            {"heading", "Downloading the latest Gw.exe"},
            {"cancellable", true},
            {"failure", "Gw.exe couldn't be updated. If the game is in a protected folder, run GW Launcher as administrator"},
        };
        const std::filesystem::path exe = exe_;
        const std::string gwpath = gwpath_;
        runJob(
            popup,
            [exe](const ProgressReport& report, const std::atomic<bool>& cancel) { updateGwClients({exe}, report, cancel); },
            [reply, gwpath](const std::optional<std::string>& failure) {
                reply.result(failure ? Json(nullptr) : Json("Gw.exe updated at " + gwpath + "."));
            });
    }

private:
    std::string gwpath_;
    std::filesystem::path exe_;
};

}  // namespace

std::unique_ptr<Issue> checkGwExeOutdated(const Account& account) {
    const auto exe = gameFilePath(account.gwpath);
    if (!exe) {
        return nullptr;
    }
    GwFileInfo latest;
    try {
        latest = cachedLatestGwInfo();
    } catch (const std::exception&) {
        // The check is quiet when ArenaNet can't be reached, as the Rust launcher's is.
        return nullptr;
    }
    if (!gwIsOutdated(*exe, latest)) {
        return nullptr;
    }
    return std::make_unique<GwExeOutdatedIssue>(account.gwpath, *exe);
}

}  // namespace gwl
