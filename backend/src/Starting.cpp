// The game paths whose accounts are starting. An account is added once its checks have passed, and removed when its start ends, so a
// second account with the same game counts as in use while the first is starting.

#include <mutex>
#include <set>
#include <string>

#include "Launch.h"

namespace gwl {

namespace {

std::mutex& startingMutex() {
    static std::mutex mutex;
    return mutex;
}

std::set<std::u16string>& startingPaths() {
    static std::set<std::u16string> paths;
    return paths;
}

}  // namespace

void markStarting(const std::u16string& gamePath) {
    std::lock_guard<std::mutex> lock(startingMutex());
    startingPaths().insert(gamePath);
}

void unmarkStarting(const std::u16string& gamePath) {
    std::lock_guard<std::mutex> lock(startingMutex());
    startingPaths().erase(gamePath);
}

bool isStarting(const std::u16string& gamePath) {
    std::lock_guard<std::mutex> lock(startingMutex());
    return startingPaths().count(gamePath) > 0;
}

}  // namespace gwl
