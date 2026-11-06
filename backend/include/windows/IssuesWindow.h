// The issues window: an account's problems, with a fix where one exists, and Launch when nothing blocks it.

#pragma once

#include "AppState.h"
#include "LauncherWindow.h"

namespace launcher {

class IssuesWindow : public LauncherWindow {
public:
    // The page's arguments for the window: the account's index, its name, the issues, and whether it can launch.
    IssuesWindow(AppState& app, Json args);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // Closing the window hides it, so it can be shown again.
    bool mayClose() override;

private:
    AppState& app_;
    Json args_;
};

}  // namespace launcher
