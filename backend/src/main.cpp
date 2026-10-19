// The launcher on the native layer: the account list and its windows, or a quick launch of one account for a -launch start.

#include <exception>
#include <string>
#include <vector>

#include "AppState.h"
#include "EmbeddedAssets.h"
#include "AccountListWindow.h"
#include "Instance.h"
#include "TempFiles.h"
#include "WebViewWindow.h"

#ifdef _WIN32
#include "Cli.h"
#endif

int main(int argc, char** argv) {
#ifdef _WIN32
    // Game-launch children must bypass single-instance forwarding and windowing setup.
    if (argc > 1 && std::string(argv[1]) == "--launch") {
        return gwl::runCli(std::vector<std::string>(argv + 1, argv + argc));
    }
#endif
    try {
        const std::vector<std::string> arguments(argv, argv + argc);
        const gwl::InstanceRequest request = gwl::InstanceRequest::fromArgs(arguments);
        // A launcher that's already running takes the request, and this start ends.
        if (gwl::forwardRequest(request)) {
            return 0;
        }
        // A quick launch doesn't listen for other starts, so other quick launches may be running and using their temp files.
        const bool quick = request.kind == gwl::InstanceRequest::Kind::Launch;
        if (!quick) {
            gwl::removeLeftoverTempFiles();
        }
        // Windows and menus need the windowing started, and the tray's menu is made before the list window.
        gwl::initializeWindowing();
        gwl::setApplicationIcon(gwl::embeddedHelmetPng());
        launcher::AppState app(gwl::embeddedPage());
        if (quick) {
            app.quickLaunch(request.name);
            gwl::runEventLoop();
            return 0;
        }
        app.launchArguments.assign(argv + 1, argv + argc);
        // Listening starts before the window is made, so a start made while the window is still opening is handed over too.
        // Later starts' requests arrive on the listener's thread, so each one is passed to the window's thread.
        gwl::listenForRequests([&app](gwl::InstanceRequest next) {
            gwl::runOnUiThread([&app, next] { app.instanceRequest(next); });
        });
        // The tray comes up before the list, which is only visible without it: with a tray the list is shown beside the icon.
        app.applyTrayMode();
        launcher::AccountListWindow list(app);
        app.list = &list;
        list.open();
        app.checkLauncherUpdate();
        if (app.tray && !app.settings.launchMinimized) {
            app.showListAtStart();
        }
        gwl::runEventLoop();
    } catch (const gwl::RuntimeMissingError&) {
        // The dialog has already said what's missing.
        return 1;
    } catch (const std::exception& error) {
        gwl::showError(error.what());
        return 1;
    }
    return 0;
}
