// The account list's context menu: a borderless window at the pointer, closed by a pick or by focus leaving it.

#pragma once

#include <chrono>

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class ContextMenuWindow : public gwl::WebViewWindow {
public:
    // The menu's items, as the page's menu lists them.
    ContextMenuWindow(AppState& app, gwl::Json items);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // A menu closes when focus leaves it, except in the first moment after it opens, so the click that opened it can't
    // close it straight away.
    void onFocusLost() override;

private:
    void installHandlers();

    AppState& app_;
    gwl::Json items_;
    std::chrono::steady_clock::time_point shownAt_ = std::chrono::steady_clock::now();
};

}  // namespace launcher
