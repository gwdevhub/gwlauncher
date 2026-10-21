// The account list: the launcher's main window.

#pragma once

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class AccountListWindow : public gwl::WebViewWindow {
public:
    explicit AccountListWindow(AppState& app);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // With the tray icon up and no title bar, closing the list hides it into the tray. Otherwise the list can't be found
    // again once closed, so closing it is allowed, and quits the launcher.
    bool allowClose() override;
    // In tray mode losing focus hides the list, as a click away from it does.
    void onFocusLost() override;

private:
    void installHandlers();

    AppState& app_;
};

}  // namespace launcher
