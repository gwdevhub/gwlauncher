// A message the page or the launcher shows, with one OK button.

#pragma once

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class MessageWindow : public gwl::WebViewWindow {
public:
    MessageWindow(AppState& app, const std::string& title, const std::string& text);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // A message stays in the list once shown, hidden, so it can be shown again.
    bool allowClose() override;

private:
    AppState& app_;
    gwl::Json content_;
};

}  // namespace launcher
