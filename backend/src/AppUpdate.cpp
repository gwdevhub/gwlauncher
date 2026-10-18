// GW Launcher's own update, as the launcher's AppState runs it: the startup check, the prompt, and the download and restart.

#include <atomic>
#include <cstddef>
#include <exception>
#include <filesystem>
#include <optional>
#include <string>
#include <thread>
#include <vector>

#include "AppState.h"
#include "LauncherUpdate.h"
#include "Progress.h"
#include "Shell.h"
#include "WebViewWindow.h"
#include "AccountListWindow.h"

namespace launcher {

void AppState::checkLauncherUpdate() {
    std::thread([this] {
        const std::filesystem::path current = gwl::executablePath();
        gwl::removeLauncherLeftovers(current);
        // A build that doesn't know its commit count doesn't check for updates.
        const std::uint32_t build = gwl::currentBuild();
        if (build == 0) {
            return;
        }
        std::optional<gwl::LauncherRelease> release;
        try {
            release = gwl::latestLauncherRelease();
        } catch (const std::exception&) {
            // The update check is quiet when GitHub can't be reached, as the Rust launcher's is.
            return;
        }
        if (!release || release->revision <= build) {
            return;
        }
        gwl::runOnUiThread([this, release] {
            availableUpdate = release;
            if (list) {
                list->emit("refresh", gwl::Json());
            }
            if (settings.promptForUpdates) {
                openLauncherUpdatePrompt(*release);
            }
        });
    }).detach();
}

void AppState::openLauncherUpdatePrompt(const gwl::LauncherRelease& release) {
    const gwl::Json prompt{
        {"title", "GW Launcher update"},
        {"message", "GW Launcher r" + std::to_string(release.revision) + " is available. This is r" + std::to_string(gwl::currentBuild()) +
                        (release.download.empty() ? ".\n\nOpen the release page?" : ".\n\nUpdate now?")},
        {"notes", release.notes},
        {"releaseUrl", release.url},
    };
    openPrompt("update", prompt, 460, 420, [this, release](std::optional<std::size_t> answer) {
        if (!answer || *answer != 1) {
            return;
        }
        if (release.download.empty()) {
            // Nothing is downloaded: the release page has the new AppImage.
            gwl::openUrl(release.url);
            return;
        }
        startLauncherUpdate(release);
    });
}

void AppState::startLauncherUpdate(const gwl::LauncherRelease& release) {
    const gwl::Json popup{
        {"title", "Updating GW Launcher"},
        {"heading", "Downloading the latest GW Launcher"},
        {"note", "The launcher restarts when the update is in place."},
        {"cancellable", true},
        {"failure", "GW Launcher couldn't be updated"},
    };
    const std::vector<std::string> arguments = launchArguments;
    runWithProgress(
        popup,
        [release, arguments](const gwl::ProgressReport& report, const std::atomic<bool>& cancel) {
            const std::filesystem::path current = gwl::executablePath();
            const std::filesystem::path staged =
                current.parent_path() / (".new" + std::filesystem::path(gwl::launcherAssetName()).extension().string());
            gwl::downloadLauncherRelease(release, staged, cancel, report);
            gwl::installLauncherRelease(current, staged, arguments);
        },
        [](const std::optional<std::string>& failure) {
            // The restarted launcher takes over, so this one exits once the update is in place.
            if (!failure) {
                gwl::stopEventLoop();
            }
        });
}

}  // namespace launcher
