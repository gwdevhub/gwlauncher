// The settings window: the general options, the master password, updates and the mod launch timeout. Closing it hides it,
// so it opens again straight away.

#pragma once

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class SettingsWindow : public gwl::WebViewWindow {
public:
    explicit SettingsWindow(AppState& app);

    std::string html() const override;
    std::string devUrl() const override;

private:
    AppState& app_;
};

}  // namespace launcher
