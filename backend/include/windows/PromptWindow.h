// A prompt the user answers: the page's dialog for a choice, or for a launcher update. The answer goes to whoever asked.

#pragma once

#include <cstdint>
#include <string>

#include "AppState.h"
#include "LauncherWindow.h"

namespace launcher {

class PromptWindow : public LauncherWindow {
public:
    PromptWindow(AppState& app, std::string view, Json prompt, int width, int height);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    // Closing the prompt without answering counts as no answer. The window then hides, so it can't be closed twice.
    bool mayClose() override;

private:
    AppState& app_;
    std::string view_;
    Json prompt_;
};

}  // namespace launcher
