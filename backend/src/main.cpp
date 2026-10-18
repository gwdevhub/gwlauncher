// The launcher on the native layer: the account list, its editor and its context menu. Launching, setup, updates and the
// tray come in later slices.

#include <exception>
#include <string>
#include <vector>

#include "AppState.h"
#include "EmbeddedAssets.h"
#include "AccountListWindow.h"
#include "Instance.h"
#include "TempFiles.h"
#include "WebViewWindow.h"

int main(int argc, char** argv) {
    try {
        const std::vector<std::string> arguments(argv, argv + argc);
        const gwl::InstanceRequest request = gwl::InstanceRequest::fromArgs(arguments);
        // A launcher that's already running takes the request, and this start ends.
        if (gwl::forwardRequest(request)) {
            return 0;
        }
        // Windows and menus need the windowing started, and the tray's menu is made before the list window.
        // This is the only launcher running, so no launch or download is using a leftover temp file.
        gwl::removeLeftoverTempFiles();
        gwl::initializeWindowing();
        gwl::setApplicationIcon(gwl::embeddedHelmetPng());
        launcher::AppState app(gwl::embeddedPage());
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
        if (request.kind == gwl::InstanceRequest::Kind::Launch) {
            app.launchNamed(request.name);
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
