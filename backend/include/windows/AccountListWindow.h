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
    // With the tray icon up, closing the list hides it into the tray. Without the icon the list can't be found again, so
    // closing it is allowed, and quits the launcher.
    bool allowClose() override;
    // In tray mode losing focus hides the list, as a click away from it does.
    void onFocusLost() override;
    // Closing the list quits the launcher.
    void onClosed() override;

private:
    void installHandlers();

    AppState& app_;
};

}  // namespace launcher
