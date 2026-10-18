// The account's game is running already, so it can't be launched again. Closing the other instance clears this on a re-check.

#include <memory>
#include <string>
#include <utility>

#include "Issue.h"
#include "Launch.h"

namespace gwl {

namespace {

class InUseIssue : public Issue {
public:
    explicit InUseIssue(std::string name) : name_(std::move(name)) {}

    int severity() const override { return kBlocking; }
    std::string message() const override {
        return name_ + " is already in use. Close the other running instance, then re-check.";
    }

private:
    std::string name_;
};

}  // namespace

std::unique_ptr<Issue> checkInUse(const Account& account) {
    const auto key = account.pathKey();
    if (!key || !(isStarting(*key) || runningGamePaths().count(*key))) {
        return nullptr;
    }
    return std::make_unique<InUseIssue>(account.name());
}

}  // namespace gwl
