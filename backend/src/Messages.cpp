#include "Messages.h"

#include <stdexcept>

namespace gwl {

bool Request::parse(const std::string& text, Request& request, std::string& error) {
    Json json = Json::parse(text, nullptr, false);
    if (json.is_discarded() || !json.is_object()) {
        error = "The request isn't a JSON object.";
        return false;
    }
    if (!json.contains("id") || !json["id"].is_number_unsigned()) {
        error = "The request has no id.";
        return false;
    }
    if (!json.contains("method") || !json["method"].is_string()) {
        error = "The request has no method.";
        return false;
    }
    request.args = json.contains("args") ? json["args"] : Json::array();
    if (!request.args.is_array()) {
        error = "The request's args aren't a list.";
        return false;
    }
    request.id = json["id"].get<std::uint64_t>();
    request.method = json["method"].get<std::string>();
    return true;
}

std::string replyScript(std::uint64_t id, const Json& result) {
    return "window.gwlReceive(" + Json{{"id", id}, {"result", result}}.dump() + ")";
}

std::string errorScript(std::uint64_t id, const std::string& message) {
    return "window.gwlReceive(" + Json{{"id", id}, {"error", message}}.dump() + ")";
}

void MessageHandler::on(const std::string& name, Method method) {
    methods_[name] = std::move(method);
}

void MessageHandler::dispatch(WebViewWindow& window, const Request& request, const Reply& reply) const {
    auto found = methods_.find(request.method);
    if (found == methods_.end()) {
        reply.error("Unknown method '" + request.method + "'.");
        return;
    }
    try {
        found->second(window, request.args, reply);
    } catch (const std::exception& error) {
        reply.error(error.what());
    }
}

}  // namespace gwl
