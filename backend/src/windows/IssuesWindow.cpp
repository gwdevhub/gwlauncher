#include "IssuesWindow.h"

#include <utility>

namespace launcher {

IssuesWindow::IssuesWindow(AppState& app, Json args)
    : LauncherWindow(WindowOptions{.title = "Issues", .width = 420, .height = 320}), app_(app), args_(std::move(args)) {
    installCommonHandlers(handler(), app_);
    handler().on("window", [this](WebViewWindow&, const Json&, const Reply& reply) {
        logRequest("window");
        reply.result(Json{{"view", "issues"}, {"args", Json::array({args_})}});
    });
    // The issues of the account at index, as the page asks for them when it refreshes.
    handler().on("issues", [this](WebViewWindow&, const Json& args, const Reply& reply) {
        logRequest("issues");
        const auto index = args.at(0).get<std::size_t>();
        if (index >= app_.accounts.size()) {
            reply.result(Json::array());
            return;
        }
        app_.checkAccount(app_.accounts[index], [reply](const gwl::IssueChecker::Issues& issues) {
            reply.result(gwl::issuesJson(issues));
        });
    });
    handler().on("fix", [this](WebViewWindow&, const Json& args, const Reply& reply) {
        logRequest("fix");
        app_.fixIssue(args.at(0).get<std::size_t>(), args.at(1), reply);
    });
}

std::string IssuesWindow::html() const {
    return app_.pageHtml();
}

std::string IssuesWindow::devUrl() const {
    return developmentUrl();
}

bool IssuesWindow::mayClose() {
    app_.endQuickIfIdle();
    return true;
}

void AppState::openIssues(const std::string& key, const Json& args) {
    auto found = issueWindows.find(key);
    if (found != issueWindows.end()) {
        found->second->present();
        return;
    }
    auto window = std::make_unique<IssuesWindow>(*this, args);
    window->open();
    issueWindows.emplace(key, std::move(window));
}

}  // namespace launcher
