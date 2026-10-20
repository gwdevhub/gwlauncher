#include "ContextMenuWindow.h"

#include "AccountListWindow.h"

#include <utility>

namespace launcher {

ContextMenuWindow::ContextMenuWindow(AppState& app, gwl::Json items)
    : WebViewWindow(gwl::WindowOptions{.title = u"", .width = 190, .height = 100, .titleBar = false, .skipTaskbar = true,
                                       .alwaysOnTop = true, .autosize = true}),
      app_(app),
      items_(std::move(items)) {
    installCommonHandlers(handler(), app_);
    installHandlers();
}

std::string ContextMenuWindow::html() const {
    return app_.pageHtml();
}

std::string ContextMenuWindow::devUrl() const {
    return developmentUrl();
}

void ContextMenuWindow::onFocusLost() {
    using namespace std::chrono_literals;
    if (std::chrono::steady_clock::now() - shownAt_ > 400ms) {
        hide();
    }
}

void ContextMenuWindow::installHandlers() {
    auto& handler = this->handler();

    handler.on("window", [this](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("window");
        reply.result(gwl::Json{{"view", "menu"}, {"args", gwl::Json::array({items_})}});
    });
    // A pick goes to the account list, which runs the action, and the menu closes.
    handler.on("menuPick", [this](gwl::WebViewWindow& window, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("menuPick");
        if (app_.list) {
            app_.list->emit("menuPick", args.at(0));
        }
        window.hide();
        reply.result(nullptr);
    });
}

// Opens the context menu at the pointer. Menus from earlier openings are closed here, from the list's own message, so
// none is destroyed while it runs.
void AppState::openMenu(const gwl::Json& items) {
    menus.clear();
    auto menu = std::make_unique<ContextMenuWindow>(*this, items);
    menu->open();
    if (const auto cursor = gwl::cursorPosition()) {
        menu->move(cursor->first, cursor->second);
    }
    menus.push_back(std::move(menu));
}

}  // namespace launcher
