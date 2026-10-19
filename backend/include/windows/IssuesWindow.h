// The issues window: an account's problems, with a fix where one exists, and Launch when nothing blocks it.

#pragma once

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class IssuesWindow : public gwl::WebViewWindow {
public:
    // The page's arguments for the window: the account's index, its name, the issues, and whether it can launch.
    IssuesWindow(AppState& app, gwl::Json args);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // Closing the window hides it, so it can be shown again.
    bool allowClose() override;

private:
    AppState& app_;
    gwl::Json args_;
};

}  // namespace launcher
