// Starting accounts from the list: the issue checks, the launch on its own thread, and the states the list shows.

#include <atomic>
#include <chrono>
#include <cctype>
#include <filesystem>
#include <future>
#include <mutex>
#include <optional>
#include <set>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>

#include <algorithm>

#include "AppState.h"
#include "GMod.h"
#include "Issue.h"
#include "GwNet.h"
#include "Launch.h"
#include "Process.h"
#include "IssueChecker.h"
#include "Progress.h"
#include "utils/Files.h"

namespace launcher {

namespace {

constexpr const char* kDefaultMissing = "Couldn't find a default installation of Guild Wars, is it installed?";

// The launch result the list expects: started, or the error the launch gave.
gwl::Json launchResult(const std::optional<std::string>& failure) {
    if (!failure) {
        return {{"started", true}, {"error", nullptr}, {"issues", nullptr}, {"canLaunch", false}};
    }
    return {{"started", false}, {"error", *failure}, {"issues", nullptr}, {"canLaunch", false}};
}

// The reply for launchNow and launchDefault: null on success, or the error text.
gwl::Json plainResult(const std::optional<std::string>& failure) {
    return failure ? gwl::Json(*failure) : gwl::Json(nullptr);
}


}  // namespace

void AppState::checkAccount(const gwl::Account& account, std::function<void(const gwl::IssueChecker::Issues&)> done) {
    gwl::WorkerPool* pool = &workers;
    // The machine is measured once for the round, on a worker, and all of the account's checks read that one measurement.
    workers.submit(
        [] { return std::make_shared<const gwl::MachineFacts>(gwl::measureMachine()); },
        [pool, account, done](std::shared_ptr<const gwl::MachineFacts> facts) {
            gwl::IssueChecker checker(account, std::move(facts));
            checker.start(*pool, [done](gwl::IssueChecker::Issues issues) {
                gwl::runOnUiThread([done, issues] { done(issues); });
            });
        });
}

// The fix is the one the issue offers. The account is checked again first, so the fix comes from the current issues.
void AppState::fixIssue(std::size_t index, const gwl::Json& fix, gwl::Reply reply) {
    if (index >= accounts.size()) {
        reply.result(nullptr);
        return;
    }
    checkAccount(accounts[index], [this, fix, reply](const gwl::IssueChecker::Issues& issues) {
        for (const auto& issue : issues) {
            if (issue->toJson() == fix) {
                issue->fix(reply, [this](const gwl::Json& popup, const gwl::ProgressWork& work, gwl::ProgressDone done) {
                    runWithProgress(popup, work, std::move(done));
                });
                return;
            }
        }
        reply.result(nullptr);
    });
}

void AppState::launch(gwl::Reply reply, const gwl::Json& args, bool suspended, bool checkIssues) {
    const bool isLaunch = checkIssues;
    const auto index = args.at(0).is_number_unsigned() ? args.at(0).get<std::size_t>() : accounts.size();
    if (index >= accounts.size()) {
        if (isLaunch) {
            reply.result(launchResult("Unknown account."));
        } else {
            reply.result(plainResult("Unknown account."));
        }
        return;
    }
    const gwl::Account account = accounts[index];
    // An account that is already starting can't be started again. A running game is an issue, checked with the others.
    if (launching.count(account.name())) {
        const std::string text = gwl::utf16ToUtf8(account.name()) + " is already starting.";
        reply.result(isLaunch ? launchResult(text) : plainResult(text));
        return;
    }
    startGameAsync(account, index, suspended, isLaunch,
                   [reply, isLaunch](const std::optional<std::string>& failure, const std::optional<gwl::Json>& blocked) {
                       if (blocked) {
                           reply.result(*blocked);
                           return;
                       }
                       // A cancelled start isn't an error: the game was never resumed.
                       if (failure == gwl::kCancelledMessage) {
                           reply.result(isLaunch ? gwl::Json{{"started", false}, {"error", nullptr}, {"issues", nullptr}, {"canLaunch", false}}
                                                 : gwl::Json(nullptr));
                           return;
                       }
                       reply.result(isLaunch ? launchResult(failure) : plainResult(failure));
                   });
}

void AppState::launchDefault(gwl::Reply reply) {
    const auto install = gwl::defaultInstall();
    if (!install) {
        reply.result(plainResult(kDefaultMissing));
        return;
    }
    gwl::Account account;
    account.gwpath = *install;
    startGameAsync(account, std::nullopt, false, false,
                   [reply](const std::optional<std::string>& failure, const std::optional<gwl::Json>&) {
                       reply.result(plainResult(failure == gwl::kCancelledMessage ? std::nullopt : failure));
                   });
}

// Checks the account and starts it behind one progress window, which opens at once. Cancel stops the start at its next step, until
// Guild Wars starts; the checks and the Steam wait run to their own end first. A suspended start waits paused until the window's
// Continue, and Cancel there ends the start without resuming the game.
void AppState::startGameAsync(const gwl::Account& account, std::optional<std::size_t> index, bool suspended, bool checkFirst,
                              std::function<void(const std::optional<std::string>&, const std::optional<gwl::Json>&)> done) {
    const std::u16string name = account.name();
    const auto key = account.pathKey();
    launching.insert(name);
    const std::uint32_t timeout = settings.timeoutOnModlaunch;
    const std::filesystem::path dir = std::filesystem::current_path();
    const gwl::Json note = checkFirst ? gwl::Json("Checking the account...")
                                      : gwl::Json(suspended ? "Guild Wars starts paused, so its plugins can load." : "Guild Wars is starting.");
    const gwl::Json popup{
        {"title", "Starting " + gwl::utf16ToUtf8(name)},
        {"heading", "Starting " + gwl::utf16ToUtf8(name)},
        {"icon", accountIconUrl(account)},
        {"note", note},
        {"cancellable", true},
        {"failure", "Guild Wars couldn't be started"},
    };
    launchContinue = std::make_shared<std::atomic<bool>>(false);
    // Set when the checks stop the launch: the page's result, with the issues.
    auto blocked = std::make_shared<std::optional<gwl::Json>>();
    runWithProgress(
        popup,
        [this, account, key, suspended, checkFirst, timeout, dir, resume = launchContinue, blocked](const gwl::ProgressReport& report,
                                                                                              const std::atomic<bool>& cancel) {
            if (checkFirst) {
                // The checks finish on several threads, and the window's reports aren't safe to send from all of them at once.
                auto reporting = std::make_shared<std::mutex>();
                gwl::IssueChecker::Progress checking = [report, reporting](std::size_t finished, std::size_t total) {
                    std::lock_guard<std::mutex> lock(*reporting);
                    report(std::to_string(finished) + " of " + std::to_string(total) + " checks",
                           total ? static_cast<double>(finished) / static_cast<double>(total) : 0.0);
                };
                // A launch must not start an out-of-date Gw.exe, so the latest version is asked for fresh rather than from the cache.
                gwl::clearCachedLatestGwInfo();
                // The measurement and the checks run on this job's thread and the workers, and the job waits for their issues.
                auto facts = std::make_shared<const gwl::MachineFacts>(gwl::measureMachine());
                auto promise = std::make_shared<std::promise<gwl::IssueChecker::Issues>>();
                std::future<gwl::IssueChecker::Issues> found = promise->get_future();
                gwl::IssueChecker checker(account, facts);
                checker.start(workers, [promise](gwl::IssueChecker::Issues issues) { promise->set_value(std::move(issues)); }, checking);
                const gwl::IssueChecker::Issues issues = found.get();
                const bool stopped = std::any_of(issues.begin(), issues.end(),
                                                 [](const std::shared_ptr<gwl::Issue>& issue) { return issue->severity() == gwl::Issue::kBlocking; });
                if (stopped) {
                    *blocked = gwl::Json{{"started", false}, {"error", nullptr}, {"issues", gwl::issuesJson(issues)}, {"canLaunch", false}};
                    return;
                }
            }
            if (cancel.load()) {
                throw std::runtime_error(gwl::kCancelledMessage);
            }
            // Once the checks pass, the game path counts as in use for any other account that shares it.
            if (key) {
                gwl::markStarting(*key);
            }
            if (account.isSteamAccount) {
                gwl::startSteamForLaunch(report, cancel);
            }
            report("Starting the game...", std::nullopt);
            // Once Guild Wars is starting it can't be stopped from here, so the window's Cancel is turned off.
            gwl::runOnUiThread([this] {
                if (progressWindow) {
                    progressWindow->emit("progress", gwl::Json{{"phase", "update"}, {"cancellable", false}, {"detail", "Starting the game..."}});
                }
            });
            if (cancel.load()) {
                throw std::runtime_error(gwl::kCancelledMessage);
            }
            std::function<bool()> confirm;
            if (suspended) {
                confirm = [this, resume, &cancel] {
                    gwl::runOnUiThread([this] {
                        if (progressWindow) {
                            progressWindow->emit("progress", gwl::Json{{"phase", "paused"}});
                        }
                    });
                    // Waits for Continue. The window's Cancel sets the job's cancel flag, which ends the start with the game still paused.
                    while (!resume->load()) {
                        if (cancel.load()) {
                            return false;
                        }
                        std::this_thread::sleep_for(std::chrono::milliseconds(50));
                    }
                    gwl::runOnUiThread([this] {
                        if (progressWindow) {
                            progressWindow->emit("progress", gwl::Json{{"phase", "update"}, {"cancellable", false}, {"detail", "Starting the game..."}});
                        }
                    });
                    return true;
                };
            }
            gwl::startGame(account, dir, timeout, confirm);
        },
        [this, name, key, index, blocked, done](const std::optional<std::string>& failure) {
            launching.erase(name);
            if (key) {
                gwl::unmarkStarting(*key);
            }
            if (!failure && !blocked->has_value() && key && index) {
                startedBy[*key] = *index;
            }
            done(failure, *blocked);
        });
}

std::vector<std::string> AppState::accountStates() const {
    const std::set<std::u16string> running = gwl::runningGamePaths();
    std::vector<std::string> states;
    for (std::size_t index = 0; index < accounts.size(); ++index) {
        const gwl::Account& account = accounts[index];
        const auto key = account.pathKey();
        if (launching.count(account.name())) {
            states.push_back("Launching");
        } else if (!key || !running.count(*key)) {
            states.push_back("Inactive");
        } else {
            const auto started = startedBy.find(*key);
            states.push_back(started != startedBy.end() && started->second == index ? "Active" : "In Use");
        }
    }
    return states;
}

void AppState::accountRemoved(std::size_t index) {
    for (auto it = startedBy.begin(); it != startedBy.end();) {
        if (it->second == index) {
            it = startedBy.erase(it);
        } else {
            if (it->second > index) {
                --it->second;
            }
            ++it;
        }
    }
}

void AppState::accountMoved(std::size_t from, std::size_t to) {
    for (auto& [key, index] : startedBy) {
        if (index == from) {
            index = to;
        } else if (from < index && index <= to) {
            --index;
        } else if (to <= index && index < from) {
            ++index;
        }
    }
}

}  // namespace launcher

namespace gwl {

// The path a running game is listed under: the Linux path, lower case. Missing when the account's path isn't on this computer.
std::optional<std::u16string> Account::pathKey() const {
    const auto linuxPath = linuxPathFor(gwpath);
    if (!linuxPath) {
        return std::nullopt;
    }
    return lowerCase(*linuxPath);
}

}  // namespace gwl
