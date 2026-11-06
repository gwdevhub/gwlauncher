// The base of the launcher's windows. The launcher keeps its windows for reuse, so closing one hides it, and it opens again straight
// away. A window that should stay open when it's closed answers through mayClose.

#pragma once

#include "Desktop.h"
#include "WebViewWindow.h"

namespace launcher {

class LauncherWindow : public WebViewWindow {
public:
    explicit LauncherWindow(WindowOptions options) : WebViewWindow(withProfileFolder(std::move(options))) {}

protected:
    // Whether the window may be closed. A window that may be closed is hidden, and one that may not stays as it is.
    virtual bool mayClose() { return true; }

private:
    static WindowOptions withProfileFolder(WindowOptions options) {
        options.webViewDataFolder = gwl::webViewDataFolder();
        return options;
    }

    bool allowClose() final {
        if (mayClose()) {
            hide();
        }
        return false;
    }
};

}  // namespace launcher
