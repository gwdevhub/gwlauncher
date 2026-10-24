#include "Instance.h"

#include "Messages.h"
#include "utils/Parsing.h"

namespace gwl {

InstanceRequest InstanceRequest::fromArgs(const std::vector<std::string>& arguments) {
    std::string name;
    if (parseArgumentWithValue(arguments, "-launch", &name)) {
        return {InstanceRequest::Kind::Launch, name};
    }
    return {};
}

std::string InstanceRequest::encode() const {
    const Json value = kind == Kind::Launch ? Json{{"request", "launch"}, {"name", name}} : Json{{"request", "show"}};
    return value.dump() + "\n";
}

std::optional<InstanceRequest> InstanceRequest::decode(const std::string& line) {
    const Json value = Json::parse(line, nullptr, false);
    if (value.is_discarded() || !value.is_object() || !value.contains("request") || !value["request"].is_string()) {
        return std::nullopt;
    }
    const std::string kind = value["request"].get<std::string>();
    if (kind == "show") {
        return InstanceRequest{};
    }
    if (kind == "launch" && value.contains("name") && value["name"].is_string()) {
        return InstanceRequest{InstanceRequest::Kind::Launch, value["name"].get<std::string>()};
    }
    return std::nullopt;
}

}  // namespace gwl
