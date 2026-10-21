#include "PromptWindow.h"

#include <utility>

namespace launcher {

PromptWindow::PromptWindow(AppState& app, std::string view, gwl::Json prompt, int width, int height)
    : WebViewWindow(gwl::WindowOptions{.title = prompt.at("title").get<std::string>(), .width = width, .height = height}),
      app_(app),
      view_(std::move(view)),
      prompt_(std::move(prompt)) {
    installCommonHandlers(handler(), app_);
    handler().on("window", [this](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("window");
        reply.result(gwl::Json{{"view", view_}, {"args", gwl::Json::array({prompt_})}});
    });
}

std::string PromptWindow::html() const {
    return app_.pageHtml();
}

std::string PromptWindow::devUrl() const {
    return developmentUrl();
}

bool PromptWindow::allowClose() {
    app_.answerChoice(prompt_["id"].get<std::uint64_t>(), std::nullopt);
    app_.endQuickIfIdle();
    return true;
}

// Prompts are asked from the page or from the launcher. The window answers through the "answer" call.
void AppState::openPrompt(const std::string& view, gwl::Json prompt, int width, int height, ChoiceDone done) {
    const std::uint64_t id = nextChoice++;
    prompt["id"] = id;
    pendingChoices.emplace(id, std::move(done));
    auto window = std::make_unique<PromptWindow>(*this, view, std::move(prompt), width, height);
    window->open();
    choiceWindows.emplace(id, std::move(window));
}

void AppState::openChoice(const std::string& title, const std::string& message, const std::vector<std::string>& options, ChoiceDone done) {
    gwl::Json choices = gwl::Json::array();
    for (const std::string& option : options) {
        choices.push_back(option);
    }
    openPrompt("choose", gwl::Json{{"title", title}, {"message", message}, {"options", choices}}, 380, 170, std::move(done));
}

void AppState::answerChoice(std::uint64_t id, std::optional<std::size_t> answer) {
    auto pending = pendingChoices.find(id);
    if (pending == pendingChoices.end()) {
        return;
    }
    ChoiceDone done = std::move(pending->second);
    pendingChoices.erase(pending);
    done(answer);
    // The window is still answering this call, so it's removed once the call returns.
    gwl::runOnUiThread([this, id] { choiceWindows.erase(id); });
}

}  // namespace launcher
