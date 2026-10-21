// The account editor, opened from the list or from another editor's Duplicate.

#pragma once

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class AccountEditWindow : public gwl::WebViewWindow {
public:
    // The page's arguments for the editor: the account's index (or null for a new one), keepDraft, and a copy to start from.
    AccountEditWindow(AppState& app, gwl::Json args);

    std::string html() const override;
    std::string devUrl() const override;

private:
    void installHandlers();

    AppState& app_;
    gwl::Json args_;
};

}  // namespace launcher
