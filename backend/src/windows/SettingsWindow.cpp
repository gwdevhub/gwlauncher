#include "SettingsWindow.h"

namespace launcher {

SettingsWindow::SettingsWindow(AppState& app)
    : LauncherWindow(WindowOptions{.title = "GWLauncher Settings", .width = 420, .height = 600, .autosize = true}), app_(app) {
    installCommonHandlers(handler(), app_);
    handler().on("window", [](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("window");
        reply.result(Json{{"view", "settings"}, {"args", Json::array()}});
    });
}

std::string SettingsWindow::html() const {
    return app_.pageHtml();
}

std::string SettingsWindow::devUrl() const {
    return developmentUrl();
}

void AppState::openSettings() {
    if (settingsWindow) {
        settingsWindow->present();
        return;
    }
    settingsWindow = std::make_unique<SettingsWindow>(*this);
    settingsWindow->open();
}

}  // namespace launcher
