// The setup view: lists the Guild Wars install the launcher can make, and shows the download's progress as it runs.

#pragma once

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class SetupWindow : public gwl::WebViewWindow {
public:
    explicit SetupWindow(AppState& app);

    std::string html() const override;
    std::string devUrl() const override;

private:
    AppState& app_;
};

}  // namespace launcher
