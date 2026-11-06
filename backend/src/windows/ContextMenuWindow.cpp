#include "ContextMenuWindow.h"

#include "AccountListWindow.h"

#include <utility>

namespace launcher {

ContextMenuWindow::ContextMenuWindow(AppState& app, Json items)
    : LauncherWindow(WindowOptions{.title = "", .width = 190, .height = 100, .titleBar = false, .skipTaskbar = true,
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
        close();
        // The list lost focus to this menu, so it hides now if the click went somewhere else.
        if (app_.list) {
            app_.list->notifyFocusLost();
        }
    }
}

void ContextMenuWindow::installHandlers() {
    auto& handler = this->handler();

    handler.on("window", [this](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("window");
        reply.result(Json{{"view", "menu"}, {"args", Json::array({items_})}});
    });
    // A pick goes to the account list, which runs the action, and the menu closes.
    handler.on("menuPick", [this](WebViewWindow& window, const Json& args, const Reply& reply) {
        logRequest("menuPick");
        if (app_.list) {
            app_.list->emit("menuPick", args.at(0));
        }
        window.close();
        reply.result(nullptr);
    });
}

// Opens the context menu at the pointer. Menus from earlier openings are closed here, from the list's own message, so
// none is destroyed while it runs.
void AppState::openMenu(const Json& items) {
    menus.clear();
    // Stored before it opens, since opening can take focus from the list while this is still running.
    menus.push_back(std::make_unique<ContextMenuWindow>(*this, items));
    auto& menu = *menus.back();
    menu.open();
    if (const auto cursor = cursorPosition()) {
        menu.move(cursor->first, cursor->second);
    }
}

}  // namespace launcher
