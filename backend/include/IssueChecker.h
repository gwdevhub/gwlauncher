// Checks one account against the machine, for the issue list and the launch. The checks that read files or run programs
// are tasks on a WorkerPool, so several accounts can be checked at once without blocking the window. The checks that only
// need the MachineFacts run inline.

#pragma once

#include <cstddef>
#include <functional>
#include <memory>
#include <vector>

#include "Accounts.h"
#include "Issue.h"
#include "MachineFacts.h"
#include "WorkerPool.h"

namespace gwl {

class IssueChecker {
public:
    using Issues = std::vector<std::shared_ptr<Issue>>;
    // Called once with every issue the account has, in list order. Runs on a worker thread.
    using Done = std::function<void(Issues)>;
    // Called after each check finishes, with how many have finished out of the total. Checks finish on several threads, so the
    // callback must be safe to call from any of them.
    using Progress = std::function<void(std::size_t finished, std::size_t total)>;

    IssueChecker(Account account, std::shared_ptr<const MachineFacts> facts);

    // Starts every check. done runs once they have all finished, on the worker that finished last, so it must hand any
    // window work to runOnUiThread. The checker can be dropped as soon as start returns.
    void start(WorkerPool& pool, Done done, Progress progress = {});

private:
    Account account_;
    std::shared_ptr<const MachineFacts> facts_;
};

// The issues as the page holds them, in list order.
Json issuesJson(const IssueChecker::Issues& issues);

}  // namespace gwl
