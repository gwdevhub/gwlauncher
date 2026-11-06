// A message the page or the launcher shows, with one OK button.

#pragma once

#include "AppState.h"
#include "LauncherWindow.h"

namespace launcher {

class MessageWindow : public LauncherWindow {
public:
    MessageWindow(AppState& app, const std::string& title, const std::string& text);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // A message stays in the list once shown, hidden, so it can be shown again.
    bool mayClose() override;

private:
    AppState& app_;
    Json content_;
};

}  // namespace launcher
