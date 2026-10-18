// The progress view: a job's stage and how far it has got, with a cancel button when the job can be cancelled.

#pragma once

#include "AppState.h"
#include "WebViewWindow.h"

namespace launcher {

class ProgressWindow : public gwl::WebViewWindow {
public:
    ProgressWindow(AppState& app, gwl::Json args);

    std::string html() const override;
    std::string devUrl() const override;

protected:
    bool allowClose() override;

private:
    AppState& app_;
    gwl::Json args_;
};

}  // namespace launcher
