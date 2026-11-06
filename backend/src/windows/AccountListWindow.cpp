#include "Accounts.h"
#include "AccountListWindow.h"

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <stdexcept>
#include <utility>

namespace launcher {

namespace {


}  // namespace

AccountListWindow::AccountListWindow(AppState& app)
    : LauncherWindow(WindowOptions{.title = "GW Launcher", .width = static_cast<int>(app.settings.listWidth), .height = static_cast<int>(app.settings.listHeight), .titleBar = app.listHasTitleBar(), .skipTaskbar = app.tray != nullptr, .visible = app.tray == nullptr}), app_(app) {
    installCommonHandlers(handler(), app_);
    installHandlers();
}

std::string AccountListWindow::html() const {
    return app_.pageHtml();
}

std::string AccountListWindow::devUrl() const {
    return developmentUrl();
}

bool AccountListWindow::mayClose() {
    if (app_.tray && !app_.listHasTitleBar()) {
        app_.hideList();
        return false;
    }
    // Closing the list quits the launcher.
    app_.rememberListPlace();
    stopEventLoop();
    return false;
}

void AccountListWindow::onFocusLost() {
    // Focus can bounce away immediately after the tray click.
    const bool pastGrace = std::chrono::steady_clock::now() - app_.listShownAt > std::chrono::milliseconds(400);
    // The list's own context menu takes focus while it is open.
    const bool menuOpen = std::any_of(app_.menus.begin(), app_.menus.end(), [](const auto& menu) { return menu->isVisible(); });
    if (!app_.tray || app_.listOpenedByRightClick || !isVisible() || !pastGrace || menuOpen) {
        return;
    }
    if (containsPointer()) {
        return;
    }
    app_.listHiddenByFocusAt = std::chrono::steady_clock::now();
    app_.hideList();
}

void AccountListWindow::installHandlers() {
    auto& handler = this->handler();

    // Pinning only matters for the tray flyout, which the native layer doesn't have yet.
    handler.on("pin", [](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("pin");
        reply.result(nullptr);
    });
    handler.on("listSize", [this](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("listSize");
        const gwl::Settings effective = app_.settings.effective(gwl::hasTray());
        reply.result(Json::array({effective.listWidth, effective.listHeight}));
    });
    // The list isn't a popup, so the page's window role is null.
    handler.on("window", [](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("window");
        reply.result(nullptr);
    });
    handler.on("remove", [this](WebViewWindow&, const Json& args, const Reply& reply) {
        logRequest("remove");
        const auto index = args.at(0).get<std::size_t>();
        Json failure = app_.changeAccounts([&] { gwl::removeAccount(app_.accounts, index); });
        if (failure.is_null()) {
            app_.accountRemoved(index);
        }
        reply.result(failure);
    });
    handler.on("move", [this](WebViewWindow&, const Json& args, const Reply& reply) {
        logRequest("move");
        const auto from = args.at(0).get<std::size_t>();
        const auto to = args.at(1).get<std::size_t>();
        Json failure = app_.changeAccounts([&] { gwl::moveAccount(app_.accounts, from, to); });
        if (failure.is_null()) {
            app_.accountMoved(from, to);
        }
        reply.result(failure);
    });
    handler.on("quit", [this](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("quit");
        app_.rememberListPlace();
        stopEventLoop();
        reply.result(nullptr);
    });
    // In tray mode the list has no taskbar button to come back from, so minimising hides it into the tray.
    handler.on("minimize", [this](WebViewWindow& window, const Json&, const Reply& reply) {
        logRequest("minimize");
        if (app_.tray) {
            app_.hideList();
        } else {
            window.minimize();
        }
        reply.result(nullptr);
    });
    // Escape hides the list. In tray mode that goes through hideList, which frees the web view after a while.
    handler.on("hide", [this](WebViewWindow& window, const Json&, const Reply& reply) {
        logRequest("hide");
        if (app_.tray) {
            app_.hideList();
        } else {
            window.hide();
        }
        reply.result(nullptr);
        app_.endQuickIfIdle();
    });
    handler.on("devtools", [](WebViewWindow& window, const Json&, const Reply& reply) {
        logRequest("devtools");
        window.openDevTools();
        reply.result(nullptr);
    });
}


}  // namespace launcher
