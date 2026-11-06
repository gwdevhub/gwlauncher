// The base of the launcher's windows. The launcher keeps its windows for reuse, so closing one hides it, and it opens again straight
// away. A window that should stay open when it's closed answers through mayClose.

#pragma once

#include "WebViewWindow.h"

namespace launcher {

class LauncherWindow : public WebViewWindow {
public:
    using WebViewWindow::WebViewWindow;

protected:
    // Whether the window may be closed. A window that may be closed is hidden, and one that may not stays as it is.
    virtual bool mayClose() { return true; }

private:
    bool allowClose() final {
        if (mayClose()) {
            hide();
        }
        return false;
    }
};

}  // namespace launcher
