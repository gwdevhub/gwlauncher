// The account list: the launcher's main window.

#pragma once

#include "AppState.h"
#include "LauncherWindow.h"

namespace launcher {

class AccountListWindow : public LauncherWindow {
public:
    explicit AccountListWindow(AppState& app);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // With the tray icon up and no title bar, closing the list hides it into the tray. Otherwise the list can't be found
    // again once closed, so closing it is allowed, and quits the launcher.
    bool mayClose() override;
    // In tray mode losing focus hides the list, as a click away from it does.
    void onFocusLost() override;
    // Acts on a focus loss that was held because the page wasn't ready yet.
    void onPageReady() override;

private:
    void installHandlers();
    void hideOnFocusLoss();

    // When focus was lost while the page wasn't ready, so the loss can be acted on once it is.
    std::chrono::steady_clock::time_point focusLostBeforeReady_{};

    AppState& app_;
};

}  // namespace launcher
