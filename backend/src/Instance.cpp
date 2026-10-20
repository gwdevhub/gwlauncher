#include "Instance.h"

#include <cstddef>

#include "Messages.h"
#include "utils/Strings.h"

namespace gwl {

InstanceRequest InstanceRequest::fromArgs(const std::vector<std::u16string>& arguments) {
    for (std::size_t i = 0; i + 1 < arguments.size(); ++i) {
        if (arguments[i] == u"-launch") {
            return {InstanceRequest::Kind::Launch, arguments[i + 1]};
        }
    }
    return {};
}

std::string InstanceRequest::encode() const {
    const Json value = kind == Kind::Launch ? Json{{"request", "launch"}, {"name", utf16ToUtf8(name)}} : Json{{"request", "show"}};
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
        return InstanceRequest{InstanceRequest::Kind::Launch, utf8ToUtf16(value["name"].get<std::string>())};
    }
    return std::nullopt;
}

}  // namespace gwl
