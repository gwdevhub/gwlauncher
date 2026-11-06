// The progress view: a job's stage and how far it has got, with a cancel button when the job can be cancelled.

#pragma once

#include "AppState.h"
#include "LauncherWindow.h"

namespace launcher {

class ProgressWindow : public LauncherWindow {
public:
    ProgressWindow(AppState& app, Json args);

    std::string html() const override;
    std::string devUrl() const override;
protected:
    bool mayClose() override;
    // A page that's loaded again shows the job's latest state.
    void onPageReady() override;

private:
    AppState& app_;
    Json args_;
};

}  // namespace launcher
