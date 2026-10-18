#include "IssueChecker.h"

#include <atomic>
#include <cstddef>
#include <exception>
#include <mutex>
#include <string>
#include <utility>

namespace gwl {

namespace {

using Found = std::vector<std::unique_ptr<Issue>>;
using Check = std::function<Found(const Account&, const MachineFacts&)>;

// A check that threw. Its outcome is unknown, so it's reported as a warning rather than as a clean account.
class CheckFailedIssue : public Issue {
public:
    explicit CheckFailedIssue(std::string reason) : reason_(std::move(reason)) {}

    int severity() const override { return kWarning; }
    std::string message() const override { return "A check couldn't finish: " + reason_; }

private:
    std::string reason_;
};

Found one(std::unique_ptr<Issue> issue) {
    Found found;
    if (issue) {
        found.push_back(std::move(issue));
    }
    return found;
}

IssueChecker::Issues toShared(Found found) {
    IssueChecker::Issues issues;
    for (auto& issue : found) {
        issues.push_back(std::shared_ptr<Issue>(std::move(issue)));
    }
    return issues;
}

// One check in list order. A live check touches files or runs programs, so it runs as a worker task. The others are
// answered from the facts inline.
struct Entry {
    bool live;
    Check check;

    // Runs the check. A check that throws is reported as a failed check, so its round still finishes.
    IssueChecker::Issues run(const Account& account, const MachineFacts& facts) const {
        try {
            return toShared(check(account, facts));
        } catch (const std::exception& error) {
            return {std::make_shared<CheckFailedIssue>(error.what())};
        } catch (...) {
            return {std::make_shared<CheckFailedIssue>("unknown error")};
        }
    }
};

const std::vector<Entry>& checks() {
    static const std::vector<Entry> entries = {
        {true, [](const Account& a, const MachineFacts&) { return checkWofProblems(a); }},
        {true, [](const Account& a, const MachineFacts&) { return one(checkGwExeMissing(a)); }},
        {false, [](const Account& a, const MachineFacts& f) { return one(checkSteamNotInstalled(a, f)); }},
        {false, [](const Account& a, const MachineFacts& f) { return one(checkProtonMissing(a, f)); }},
        {false, [](const Account& a, const MachineFacts& f) { return one(checkWineMissing(a, f)); }},
        {false, [](const Account& a, const MachineFacts&) { return one(checkGModMissing(a)); }},
        {true, [](const Account& a, const MachineFacts&) { return checkModsMissing(a); }},
        {true, [](const Account& a, const MachineFacts&) { return checkModsInvalid(a); }},
        {true, [](const Account& a, const MachineFacts&) { return checkTexmodsInvalid(a); }},
        {true, [](const Account& a, const MachineFacts&) { return checkLaunchFolders(a); }},
        {true, [](const Account& a, const MachineFacts&) { return one(checkGwExeOutdated(a)); }},
        {true, [](const Account& a, const MachineFacts&) { return one(checkInUse(a)); }},
    };
    return entries;
}

// One account's checks in flight. slots[i] is check i's issues, and remaining counts the live checks not yet finished.
// Both are guarded by mutex. The account and the facts are copied into each task, so nothing mutable is shared.
struct Round {
    std::mutex mutex;
    std::vector<IssueChecker::Issues> slots;
    std::size_t remaining = 0;
    IssueChecker::Done done;
    IssueChecker::Progress progress;
    std::size_t total = 0;
    std::atomic<std::size_t> finished{0};

    // Counts one finished check, and reports the count. Called by the thread that finished the check.
    void advance() {
        const std::size_t count = ++finished;
        if (progress) {
            progress(count, total);
        }
    }

    // Records check index's issues. Returns true for the last live check to finish, which must call finish.
    bool store(std::size_t index, IssueChecker::Issues issues) {
        std::lock_guard<std::mutex> lock(mutex);
        slots[index] = std::move(issues);
        return --remaining == 0;
    }

    // Hands the issues to done, in list order. Called once, by the last check to finish.
    void finish() {
        IssueChecker::Done handOff;
        IssueChecker::Issues all;
        {
            std::lock_guard<std::mutex> lock(mutex);
            handOff = std::move(done);
            for (const auto& slot : slots) {
                all.insert(all.end(), slot.begin(), slot.end());
            }
        }
        handOff(std::move(all));
    }
};

}  // namespace

IssueChecker::IssueChecker(Account account, std::shared_ptr<const MachineFacts> facts)
    : account_(std::move(account)), facts_(std::move(facts)) {}

void IssueChecker::start(WorkerPool& pool, Done done, Progress progress) {
    const std::vector<Entry>& entries = checks();
    auto round = std::make_shared<Round>();
    round->done = std::move(done);
    round->progress = std::move(progress);
    round->total = entries.size();
    round->slots.resize(entries.size());

    // The static checks run here, before any task is queued, so writing their slots needs no lock.
    std::vector<std::size_t> live;
    for (std::size_t i = 0; i < entries.size(); ++i) {
        if (entries[i].live) {
            live.push_back(i);
        } else {
            round->slots[i] = entries[i].run(account_, *facts_);
            round->advance();
        }
    }
    round->remaining = live.size();
    if (live.empty()) {
        pool.submit([round] { round->finish(); });
        return;
    }
    for (std::size_t i : live) {
        pool.submit([round, i, account = account_, facts = facts_] {
            IssueChecker::Issues issues = checks()[i].run(account, *facts);
            round->advance();
            if (round->store(i, std::move(issues))) {
                round->finish();
            }
        });
    }
}

Json issuesJson(const IssueChecker::Issues& issues) {
    Json list = Json::array();
    for (const auto& issue : issues) {
        list.push_back(issue->toJson());
    }
    return list;
}

}  // namespace gwl
