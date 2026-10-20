// A -launch start with no launcher running: the account's start and the windows it needs, without the list.

#include <string>

#include "AppState.h"

namespace launcher {

void AppState::quickLaunch(const std::u16string& name) {
    quick = true;
    if (accountsLocked) {
        quickAccount = name;
        openPrompt("password", gwl::Json{{"title", "GW Launcher"}}, 380, 240, [this](std::optional<std::size_t>) { endQuickIfIdle(); });
        return;
    }
    if (!accountsError.empty()) {
        openMessage("GW Launcher", accountsError);
        return;
    }
    launchNamed(name);
}

void AppState::endQuickIfIdle() {
    if (!quick || !launching.empty()) {
        return;
    }
    const auto shown = [](const auto& windows) {
        for (const auto& [key, window] : windows) {
            if (window->isVisible()) {
                return true;
            }
        }
        return false;
    };
    if ((progressWindow && progressWindow->isVisible()) || shown(issueWindows) || shown(messageWindows) || shown(choiceWindows)) {
        return;
    }
    gwl::stopEventLoop();
}

}  // namespace launcher
