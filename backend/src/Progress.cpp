#include <chrono>
#include <memory>
#include <mutex>
#include <optional>
#include <string>
#include <thread>
#include <utility>

#include "AppState.h"
#include "Progress.h"
#include "windows/ProgressWindow.h"
#include "utils/Errors.h"

namespace gwl {

namespace {

const std::chrono::milliseconds kProgressThrottle{100};

}  // namespace

ProgressReport throttled(ProgressReport send) {
    // A report that comes too soon after the last one is held, and the newest held report is sent once the interval has passed, so the
    // window always ends on the latest stage and fraction rather than on whichever report happened to come first.
    struct State {
        std::mutex mutex;
        std::optional<std::chrono::steady_clock::time_point> last;
        std::optional<std::pair<std::string, std::optional<double>>> held;
        bool flushing = false;
    };
    auto state = std::make_shared<State>();
    auto sender = std::make_shared<ProgressReport>(std::move(send));
    return [state, sender](const std::string& stage, std::optional<double> fraction) {
        std::unique_lock<std::mutex> lock(state->mutex);
        const auto now = std::chrono::steady_clock::now();
        if (!state->last || now - *state->last >= kProgressThrottle) {
            state->last = now;
            state->held.reset();
            lock.unlock();
            (*sender)(stage, fraction);
            return;
        }
        state->held = std::make_pair(stage, fraction);
        if (state->flushing) {
            return;
        }
        state->flushing = true;
        const auto due = *state->last + kProgressThrottle;
        std::thread([state, sender, due] {
            std::this_thread::sleep_until(due);
            std::unique_lock<std::mutex> lock(state->mutex);
            std::optional<std::pair<std::string, std::optional<double>>> latest = std::move(state->held);
            state->held.reset();
            state->flushing = false;
            if (latest) {
                state->last = std::chrono::steady_clock::now();
            }
            lock.unlock();
            if (latest) {
                (*sender)(latest->first, latest->second);
            }
        }).detach();
    };
}

}  // namespace gwl

namespace launcher {

void AppState::runWithProgress(const gwl::Json& popup, const gwl::ProgressWork& work, gwl::ProgressDone done,
                               std::function<std::string()> summary) {
    const std::uint64_t job = ++progressJob;
    auto cancel = std::make_shared<std::atomic<bool>>(false);
    progressCancel = cancel;
    // A progress window without an icon of its own shows the launcher's helmet.
    gwl::Json shown = popup;
    if (!shown.contains("icon")) {
        shown["icon"] = helmetIcon();
    }
    progressWindow = std::make_unique<ProgressWindow>(*this, shown);
    progressWindow->open();

    const std::string failureText = popup.at("failure").get<std::string>();
    const std::string finishedText = popup.contains("finished") ? popup.at("finished").get<std::string>() : std::string();
    std::thread([this, job, failureText, finishedText, summary, work, cancel, done = std::move(done)] {
        const gwl::ProgressReport report = gwl::throttled([this, job](const std::string& stage, std::optional<double> fraction) {
            gwl::runOnUiThread([this, job, stage, fraction] {
                if (job == progressJob && progressWindow) {
                    progressWindow->emit("progress", gwl::Json{{"phase", "update"}, {"fraction", fraction ? gwl::Json(*fraction) : gwl::Json(nullptr)}, {"detail", stage}});
                }
            });
        });
        std::optional<std::string> failure;
        const auto error = gwl::captureError([&] { work(report, *cancel); });
        if (!error.empty()) failure = error;
        gwl::runOnUiThread([this, job, failure, failureText, finishedText, summary, done] {
            // A cancelled job ends quietly. Any other failure shows its reason in the window. A finished job with a message stays
            // open until the user dismisses it.
            const bool cancelled = failure && *failure == gwl::kCancelledMessage;
            const std::string finished = summary && !failure ? summary() : finishedText;
            if (job == progressJob && progressWindow) {
                if (failure && !cancelled) {
                    progressWindow->emit("progress", gwl::Json{{"phase", "failed"}, {"text", failureText + ": " + *failure}});
                } else if (!failure && !finished.empty()) {
                    progressWindow->emit("progress", gwl::Json{{"phase", "finished"}, {"text", finished}});
                } else {
                    progressWindow->emit("progress", gwl::Json{{"phase", "end"}});
                }
            }
            done(failure);
        });
    }).detach();
}

void AppState::cancelProgress() {
    if (progressCancel) {
        *progressCancel = true;
    }
}

}  // namespace launcher
