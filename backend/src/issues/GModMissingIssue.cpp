// An active texmod needs gMod.dll beside the launcher, and it isn't there. The fix downloads it, and the reply comes once
// the download has finished.

#include <atomic>
#include <filesystem>
#include <stdexcept>
#include <string>

#include "GMod.h"
#include "Issue.h"
#include "Launch.h"
#include "MachineFacts.h"
#include "ModFiles.h"
#include "Process.h"
#include "Progress.h"
#include "WebViewWindow.h"
#include "utils/Files.h"

namespace gwl {

namespace {

class GModMissingIssue : public Issue {
public:
    int severity() const override { return kBlocking; }
    std::string message() const override { return "Texmods need gMod.dll, which isn't next to the launcher."; }

    void describe(Json& json) const override { json["canDownloadGMod"] = true; }

    void fix(Reply reply, const JobRunner& runJob) override {
        const std::string folder = pathToUtf8(std::filesystem::current_path());
        const Json popup{
            {"title", "Downloading gMod"},
            {"heading", "Downloading gMod.dll"},
            {"note", "Texture mods need gMod.dll, which is saved beside the launcher."},
            {"cancellable", true},
            {"failure", "gMod.dll couldn't be downloaded"},
        };
        runJob(
            popup,
            [folder](const ProgressReport& report, const std::atomic<bool>& cancel) { downloadGMod(folder, report, cancel); },
            [reply](const std::optional<std::string>& failure) {
                reply.result(failure ? Json(nullptr) : Json("gMod.dll is downloaded. Launch the account again."));
            });
    }
};

}  // namespace

std::unique_ptr<Issue> checkGModMissing(const Account& account) {
    // Texture mods run through gMod, which the launch looks for in the launcher's folder, as the Rust launcher does.
    const auto folder = std::filesystem::current_path();
    const ModSelection selection = accountSelection(account, folder, findGmod(folder, folder));
    if (selection.texmods.empty() || !selection.needsGmod()) {
        return nullptr;
    }
    return std::make_unique<GModMissingIssue>();
}

}  // namespace gwl
