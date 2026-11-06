#include "ProgressWindow.h"

namespace launcher {

ProgressWindow::ProgressWindow(AppState& app, Json args)
    : LauncherWindow(WindowOptions{.title = args.at("title").get<std::string>(), .width = 420, .height = 190, .titleBar = false, .resizable = false, .autosize = true}), app_(app),
      args_(std::move(args)) {
    installCommonHandlers(handler(), app_);
    handler().on("window", [this](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("window");
        reply.result(Json{{"view", "progress"}, {"args", Json::array({args_})}});
    });
}

std::string ProgressWindow::html() const {
    return app_.pageHtml();
}

std::string ProgressWindow::devUrl() const {
    return developmentUrl();
}

bool ProgressWindow::mayClose() {
    app_.endQuickIfIdle();
    return true;
}

void ProgressWindow::onPageReady() {
    if (!app_.progressState.is_null()) {
        emit("progress", app_.progressState);
    }
}

}  // namespace launcher
