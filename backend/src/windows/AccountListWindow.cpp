#include "Accounts.h"
#include "AccountListWindow.h"

#include <chrono>
#include <cstdint>
#include <stdexcept>
#include <utility>

namespace launcher {

namespace {


}  // namespace

AccountListWindow::AccountListWindow(AppState& app)
    : WebViewWindow(gwl::WindowOptions{.title = "GW Launcher", .width = static_cast<int>(app.settings.listWidth), .height = static_cast<int>(app.settings.listHeight), .titleBar = app.listHasTitleBar(), .skipTaskbar = app.tray != nullptr, .visible = app.tray == nullptr}), app_(app) {
    installCommonHandlers(handler(), app_);
    installHandlers();
}

std::string AccountListWindow::html() const {
    return app_.pageHtml();
}

std::string AccountListWindow::devUrl() const {
    return developmentUrl();
}

bool AccountListWindow::allowClose() {
    if (app_.tray && !app_.listHasTitleBar()) {
        app_.hideList();
        return false;
    }
    app_.rememberListPlace();
    return true;
}

void AccountListWindow::onFocusLost() {
    // In tray mode a list that loses focus hides, unless a right click opened it, the pointer is on it, or the focus change came right after the tray click.
    // Focus can bounce away from a list the tray click has just shown, so losing it within 400 ms isn't a click away.
    const bool pastGrace = std::chrono::steady_clock::now() - app_.listShownAt > std::chrono::milliseconds(400);
    if (!app_.tray || app_.listOpenedByRightClick || !isVisible() || !pastGrace) {
        return;
    }
    const auto pointer = gwl::cursorPosition();
    const auto frame = this->frame();
    if (pointer && frame && frame->contains(pointer->first, pointer->second)) {
        return;
    }
    app_.listHiddenByFocusAt = std::chrono::steady_clock::now();
    app_.hideList();
}

void AccountListWindow::onClosed() {
    gwl::stopEventLoop();
}

void AccountListWindow::installHandlers() {
    auto& handler = this->handler();

    // Pinning only matters for the tray flyout, which the native layer doesn't have yet.
    handler.on("pin", [](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("pin");
        reply.result(nullptr);
    });
    handler.on("listSize", [this](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("listSize");
        const gwl::Settings effective = app_.settings.effective(gwl::hasTray());
        reply.result(gwl::Json::array({effective.listWidth, effective.listHeight}));
    });
    // The list isn't a popup, so the page's window role is null.
    handler.on("window", [](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("window");
        reply.result(nullptr);
    });
    handler.on("remove", [this](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("remove");
        const auto index = args.at(0).get<std::size_t>();
        gwl::Json failure = app_.changeAccounts([&] { gwl::removeAccount(app_.accounts, index); });
        if (failure.is_null()) {
            app_.accountRemoved(index);
        }
        reply.result(failure);
    });
    handler.on("move", [this](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("move");
        const auto from = args.at(0).get<std::size_t>();
        const auto to = args.at(1).get<std::size_t>();
        gwl::Json failure = app_.changeAccounts([&] { gwl::moveAccount(app_.accounts, from, to); });
        if (failure.is_null()) {
            app_.accountMoved(from, to);
        }
        reply.result(failure);
    });
    handler.on("quit", [this](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("quit");
        app_.rememberListPlace();
        gwl::stopEventLoop();
        reply.result(nullptr);
    });
    // In tray mode the list has no taskbar button to come back from, so minimising hides it into the tray.
    handler.on("minimize", [this](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("minimize");
        if (app_.tray) {
            app_.hideList();
        } else {
            window.minimize();
        }
        reply.result(nullptr);
    });
    handler.on("devtools", [](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("devtools");
        window.openDevTools();
        reply.result(nullptr);
    });
}


}  // namespace launcher
