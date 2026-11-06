#pragma once

#include <exception>
#include <string>

namespace gwl {

// An empty result means success; callers choose whether a failure is recoverable or fatal.
template <typename Work>
std::string captureError(Work&& work, const std::string& context = {}) {
    try {
        work();
        return {};
    } catch (const std::exception& error) {
        return context + (*error.what() ? error.what() : "unknown error");
    } catch (...) {
        return context + "unknown error";
    }
}

}  // namespace gwl
