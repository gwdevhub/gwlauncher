#pragma once

#include <cstdint>
#include <functional>
#include <map>
#include <string>

#include <nlohmann/json.hpp>

namespace gwl {

using Json = nlohmann::json;

// A request from the page: {"id": n, "method": "...", "args": [...]}.
struct Request {
    std::uint64_t id = 0;
    std::string method;
    Json args = Json::array();

    // Parses the text the page posted. On failure, returns false and says why in error.
    static bool parse(const std::string& text, Request& request, std::string& error);
};

// The scripts that deliver an answer to the page: window.gwlReceive({id, result}) or window.gwlReceive({id, error}).
std::string replyScript(std::uint64_t id, const Json& result);
std::string errorScript(std::uint64_t id, const std::string& message);

// Answers one request, now or later. Answer each request once.
struct Reply {
    std::function<void(const Json& result)> result;
    std::function<void(const std::string& message)> error;
};

class WebViewWindow;

// Routes requests to methods by name. A missing method, or one that throws, is answered with an error.
class MessageHandler {
public:
    using Method = std::function<void(WebViewWindow& window, const Json& args, const Reply& reply)>;

    void on(const std::string& name, Method method);
    void dispatch(WebViewWindow& window, const Request& request, const Reply& reply) const;

private:
    std::map<std::string, Method> methods_;
};

}  // namespace gwl
