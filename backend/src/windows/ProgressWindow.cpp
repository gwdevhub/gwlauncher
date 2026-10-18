#include "ProgressWindow.h"

namespace launcher {

ProgressWindow::ProgressWindow(AppState& app, gwl::Json args)
    : WebViewWindow(gwl::WindowOptions{.title = args.at("title").get<std::string>(), .width = 420, .height = 190, .decorations = false}), app_(app),
      args_(std::move(args)) {
    installCommonHandlers(handler(), app_);
    handler().on("window", [this](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("window");
        reply.result(gwl::Json{{"view", "progress"}, {"args", gwl::Json::array({args_})}});
    });
}

std::string ProgressWindow::html() const {
    return app_.pageHtml();
}

std::string ProgressWindow::devUrl() const {
    return developmentUrl();
}

bool ProgressWindow::allowClose() {
    hide();
    return false;
}

}  // namespace launcher
