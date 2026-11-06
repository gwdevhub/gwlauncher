#include "SetupWindow.h"

namespace launcher {

SetupWindow::SetupWindow(AppState& app)
    : LauncherWindow(WindowOptions{.title = "Setup", .width = 420, .height = 360}), app_(app) {
    installCommonHandlers(handler(), app_);
    handler().on("window", [](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("window");
        reply.result(Json{{"view", "setup"}, {"args", Json::array()}});
    });
}

std::string SetupWindow::html() const {
    return app_.pageHtml();
}

std::string SetupWindow::devUrl() const {
    return developmentUrl();
}

void AppState::openSetup() {
    if (setupWindow) {
        setupWindow->present();
        return;
    }
    setupWindow = std::make_unique<SetupWindow>(*this);
    setupWindow->open();
}

}  // namespace launcher
