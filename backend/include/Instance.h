// One launcher runs at a time. A start that finds one running hands its request over and ends. The request format is
// shared; each platform supplies how the request reaches the running launcher.

#pragma once

#include <functional>
#include <optional>
#include <string>
#include <vector>

namespace gwl {

struct InstanceRequest {
    enum class Kind { Show, Launch };

    Kind kind = Kind::Show;
    // The account to launch, for Launch.
    std::u16string name;

    // The request a start makes: -launch <name> launches that account, and any other start shows the list.
    static InstanceRequest fromArgs(const std::vector<std::u16string>& arguments);

    // One line of JSON, with its newline. decode reads that line back, and refuses anything else.
    std::string encode() const;
    static std::optional<InstanceRequest> decode(const std::string& line);
};

// Hands the request to the running launcher. False when none answers, so this start should carry on as the launcher.
bool forwardRequest(const InstanceRequest& request);

// Accepts requests from later starts on a thread, and passes each one to handle, which runs on that thread.
void listenForRequests(std::function<void(InstanceRequest)> handle);

}  // namespace gwl
