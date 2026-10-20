#include "SetupWindow.h"

namespace launcher {

SetupWindow::SetupWindow(AppState& app)
    : WebViewWindow(gwl::WindowOptions{.title = u"Setup", .width = 420, .height = 360}), app_(app) {
    installCommonHandlers(handler(), app_);
    handler().on("window", [](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("window");
        reply.result(gwl::Json{{"view", "setup"}, {"args", gwl::Json::array()}});
    });
}

std::string SetupWindow::html() const {
    return app_.pageHtml();
}

std::string SetupWindow::devUrl() const {
    return developmentUrl();
}

bool SetupWindow::allowClose() {
    hide();
    return false;
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
