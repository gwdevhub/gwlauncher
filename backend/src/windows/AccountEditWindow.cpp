#include "AccountEditWindow.h"

#include <optional>
#include <utility>
#include <variant>
#include <vector>

#include "Accounts.h"

namespace launcher {

AccountEditWindow::AccountEditWindow(AppState& app, gwl::Json args)
    : WebViewWindow(gwl::WindowOptions{.title = "Account", .width = 520, .height = 420}), app_(app), args_(std::move(args)) {
    installCommonHandlers(handler(), app_);
    installHandlers();
}

std::string AccountEditWindow::html() const {
    return app_.pageHtml();
}

std::string AccountEditWindow::devUrl() const {
    return developmentUrl();
}

void AccountEditWindow::installHandlers() {
    auto& handler = this->handler();

    handler.on("window", [this](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("window");
        reply.result(gwl::Json{{"view", "editor"}, {"args", args_}});
    });
    handler.on("draft", [this](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("draft");
        const gwl::Account* account = nullptr;
        if (args.at(0).is_number_unsigned() && args.at(0).get<std::size_t>() < app_.accounts.size()) {
            account = &app_.accounts[args.at(0).get<std::size_t>()];
        }
        reply.result(account ? account->draft() : gwl::Account::newDraft(gwl::defaultInstall().value_or("")));
    });
    handler.on("save", [this](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("save");
        auto result = gwl::Account::fromDraft(args.at(1));
        if (auto* errors = std::get_if<std::vector<gwl::FieldError>>(&result)) {
            reply.result(gwl::failureJson(*errors));
            return;
        }
        std::optional<std::size_t> index;
        if (args.at(0).is_number_unsigned()) {
            index = args.at(0).get<std::size_t>();
        }
        gwl::Account account = std::get<gwl::Account>(std::move(result));
        reply.result(app_.changeAccounts([&] { gwl::putAccount(app_.accounts, index, account); }));
    });
}

// The editor's window is opened from the list, or from another editor. Opening one that's already open shows it.
void AppState::openEditor(const std::string& key, const gwl::Json& args) {
    auto found = editors.find(key);
    if (found != editors.end()) {
        found->second->present();
        return;
    }
    auto editor = std::make_unique<AccountEditWindow>(*this, args);
    editor->open();
    editors.emplace(key, std::move(editor));
}

}  // namespace launcher
