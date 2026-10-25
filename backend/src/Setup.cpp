// Installing Guild Wars and updating Gw.exe. The install reports through the setup status; the update runs behind the progress
// window.

#include <atomic>
#include <chrono>
#include <cstddef>
#include <filesystem>
#include <optional>
#include <set>
#include <string>
#include <thread>
#include <utility>
#include <vector>

#include "AppState.h"
#include "GwInstall.h"
#include "GwNet.h"
#include "Launch.h"
#include "Progress.h"
#include "utils/Strings.h"
#include "utils/Files.h"

namespace launcher {

namespace fs = std::filesystem;

namespace {

}  // namespace

gwl::Json AppState::setupItems() const {
    bool installed = gwl::defaultInstall().has_value();
    for (const gwl::Account& account : accounts) {
        const auto exe = gwl::gameFilePath(account.gwpath);
        std::error_code error;
        if (exe && fs::is_regular_file(*exe, error)) {
            installed = true;
        }
    }
    return gwl::Json::array({gwl::Json{
        {"title", "Guild Wars client"},
        {"description", "Downloads Gw.exe into the folder below and records it as the default install, so accounts can launch."},
        {"done", installed},
        {"canInstall", true},
        {"folder", gwl::pathToUtf8(gwl::defaultGameFolder())},
    }});
}

gwl::Json AppState::startSetup(const std::string& requested) {
    const std::string text = gwl::trimmed(requested);
    const fs::path folder = text.empty() ? gwl::defaultGameFolder() : gwl::pathFromUtf8(text);
    if (!folder.is_absolute()) {
        return "Choose a full folder path to install Guild Wars into.";
    }
    if (setup.running) {
        return nullptr;
    }
    setup = SetupState{true, "Starting the download...", std::nullopt};

    // Setup has no cancel button, so its flag is never set.
    static std::atomic<bool> noCancel{false};
    std::thread([this, folder] {
        const gwl::ProgressReport report = gwl::throttled([this](const std::string& stage, std::optional<double>) {
            gwl::runOnUiThread([this, stage] {
                if (setup.running) {
                    setup.line = stage;
                }
            });
        });
        std::optional<std::string> failure;
        try {
            const fs::path exe = gwl::installGw(folder, report, noCancel);
            gwl::setDefaultGame(exe);
        } catch (const std::exception& error) {
            failure = error.what();
        }
        gwl::runOnUiThread([this, failure] {
            setup.running = false;
            forgetInstallState();
            if (failure) {
                setup.error = "Guild Wars couldn't be installed: " + *failure;
            } else {
                setup.line = "Guild Wars is installed.";
            }
        });
    }).detach();
    return nullptr;
}

gwl::Json AppState::setupStatus() const {
    return gwl::Json{
        {"running", setup.running},
        {"lastLine", setup.line},
        {"logPath", ""},
        {"error", setup.error ? gwl::Json(*setup.error) : gwl::Json(nullptr)},
    };
}

namespace {

// The result of checking the installs: how many there were, and how many were replaced.
std::string updateSummary(std::size_t checked, std::size_t replaced) {
    const std::string count = "Checked " + std::to_string(checked) + " installations; ";
    return replaced == 0 ? count + "All up-to-date!" : count + std::to_string(replaced) + " updated";
}

}  // namespace

void AppState::startUpdateAll(gwl::Reply reply) {
    std::set<std::string> seen;
    std::vector<fs::path> paths;
    for (const gwl::Account& account : accounts) {
        if (!seen.insert(account.gwpath).second) {
            continue;
        }
        if (const auto exe = gwl::gameFilePath(account.gwpath)) {
            paths.push_back(*exe);
        }
    }
    const gwl::Json popup{
        {"title", "Updating Gw.exe"},
        {"heading", "Checking the latest Gw.exe from ArenaNet"},
        {"cancellable", true},
        {"failure", "Gw.exe couldn't be updated"},
    };
    const std::size_t checked = paths.size();
    auto replaced = std::make_shared<std::size_t>(0);
    runWithProgress(
        popup,
        [paths, replaced](const gwl::ProgressReport& report, const std::atomic<bool>& cancel) {
            *replaced = gwl::updateGwClients(paths, report, cancel);
        },
        [reply, replaced, checked](const std::optional<std::string>& failure) {
            reply.result(failure ? gwl::Json("Gw.exe update was cancelled or failed.") : gwl::Json(updateSummary(checked, *replaced)));
        },
        [replaced, checked] { return updateSummary(checked, *replaced); });
}

}  // namespace launcher
