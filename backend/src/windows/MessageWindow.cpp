#include "MessageWindow.h"
#include "utils/Strings.h"

namespace launcher {

MessageWindow::MessageWindow(AppState& app, const std::string& title, const std::string& text)
    : WebViewWindow(gwl::WindowOptions{.title = gwl::utf8ToUtf16(title), .width = 380, .height = 150}), app_(app), content_{{"title", title}, {"text", text}} {
    installCommonHandlers(handler(), app_);
    handler().on("window", [this](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("window");
        reply.result(gwl::Json{{"view", "message"}, {"args", gwl::Json::array({content_})}});
    });
}

std::string MessageWindow::html() const {
    return app_.pageHtml();
}

std::string MessageWindow::devUrl() const {
    return developmentUrl();
}

bool MessageWindow::allowClose() {
    hide();
    app_.endQuickIfIdle();
    return false;
}

void AppState::openMessage(const std::string& title, const std::string& text) {
    const std::string key = title + "\n" + text;
    auto found = messageWindows.find(key);
    if (found != messageWindows.end()) {
        found->second->present();
        return;
    }
    auto window = std::make_unique<MessageWindow>(*this, title, text);
    window->open();
    messageWindows.emplace(key, std::move(window));
}

}  // namespace launcher
