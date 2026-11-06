// The setup view: lists the Guild Wars install the launcher can make, and shows the download's progress as it runs.

#pragma once

#include "AppState.h"
#include "LauncherWindow.h"

namespace launcher {

class SetupWindow : public LauncherWindow {
public:
    explicit SetupWindow(AppState& app);

    std::string html() const override;
    std::string devUrl() const override;

private:
    AppState& app_;
};

}  // namespace launcher
