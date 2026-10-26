#include "AppState.h"

#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <filesystem>
#include <cstdlib>
#include <exception>
#include <mutex>
#include <stdexcept>
#include <utility>

#include "AccountEditWindow.h"
#include "LauncherUpdate.h"
#include "SystemInfo.h"
#include "AccountListWindow.h"
#include "DataUrl.h"
#include "EmbeddedAssets.h"
#include "Launch.h"
#include "ModFiles.h"
#include "PasswordCache.h"
#include "Shell.h"
#include "SteamIcon.h"
#include "Stylesheets.h"
#include "utils/Errors.h"
#include "utils/Files.h"

namespace launcher {

namespace {

// The folder a file picker opens in. A mod picker starts in the first plugin folder of the game that exists, or the launcher's folder.
// The Gw.exe picker starts in the folder of the account's Gw.exe, then the detected install, then the default game folder.
std::string pickerStartFolder(bool modsOnly, const std::string& gamePath) {
    namespace fs = std::filesystem;
    std::error_code error;
    const std::optional<fs::path> game = gamePath.empty() ? std::nullopt : std::optional<fs::path>(gwl::pathFromUtf8(gamePath));
    if (modsOnly) {
        for (const fs::path& folder : gwl::pluginFolders(game, fs::current_path())) {
            if (fs::is_directory(folder, error)) {
                return gwl::pathToUtf8(folder);
            }
        }
        return gwl::pathToUtf8(fs::current_path());
    }
    if (game && fs::is_regular_file(*game, error)) {
        return gwl::pathToUtf8(game->parent_path());
    }
    if (const auto install = gwl::defaultInstall()) {
        return gwl::pathToUtf8(gwl::pathFromUtf8(*install).parent_path());
    }
    const fs::path folder = gwl::defaultGameFolder();
    return fs::is_directory(folder, error) ? gwl::pathToUtf8(folder) : std::string();
}

}  // namespace


namespace {

std::filesystem::path configFolder() {
#ifdef _WIN32
    return gwl::launcherDataDir();
#else
    return std::filesystem::current_path();
#endif
}

// File dialogs can change the working directory, so configuration paths must be absolute.
const std::string kSettingsFile = gwl::pathToUtf8(configFolder() / "Settings.json");
const std::string kAccountsFile = gwl::pathToUtf8(configFolder() / "Accounts.json");
bool accountsReloading = false;

// The one account list shares its cache with a worker, independently of its WebView's cooldown/recreation.
struct AccountListCache {
    std::mutex mutex;
    std::set<std::string> running;
    std::string warning;
    bool pending = false;
    std::chrono::steady_clock::time_point started{};
};

AccountListCache accountListCache;

// Starts the Steam icon's download when a Steam account is listed. Accounts added during the session start it too.
void fetchSteamIconFor(const std::vector<gwl::Account>& accounts, const std::function<void()>& done) {
    if (std::any_of(accounts.begin(), accounts.end(), [](const gwl::Account& account) { return account.isSteamAccount; })) {
        gwl::fetchSteamIcon(done);
    }
}

}  // namespace

AppState::AppState(std::string pageHtml) : page(std::move(pageHtml)) {
    executable = gwl::executablePath();
    try {
        settings = gwl::loadSettings(kSettingsFile);
    } catch (const std::exception& error) {
        gwl::showError(error.what());
    }
    // Settings, accounts and Guild Wars downloads are kept in the folder the launcher runs from, so without a writable folder none
    // of them save.
    if (const auto folder = gwl::executablePath().parent_path(); !gwl::isWritableFolder(folder)) {
        gwl::showError("Launcher folder isn't writable: " + gwl::pathToUtf8(folder) +
                       ". Move the launcher or change the folder's permissions to save settings, accounts and downloads.");
    }
    gwl::setInstallPaths(settings.steamPath, settings.winePath);
    gwl::setUiScale(static_cast<int>(settings.uiScale));
    openAccountsFile();
    fetchSteamIconFor(accounts, [this] {
        if (list) {
            list->emit("refresh", gwl::Json());
        }
    });
}

void AppState::openAccountsFile() {
    try {
        auto opened = gwl::openAccounts(kAccountsFile, std::nullopt);
        if (!opened) {
            // A remembered password opens the file without asking. One that no longer works is forgotten.
            if (auto remembered = gwl::rememberedPassword()) {
                opened = gwl::openAccounts(kAccountsFile, remembered);
                if (!opened) {
                    gwl::forgetPassword();
                }
            }
        }
        if (!opened) {
            accountsLocked = true;
            accountsError = "Accounts.json is locked until its master password is entered.";
            return;
        }
        accounts = std::move(opened->accounts);
        accountsPassword = opened->password;
        accountsLocked = false;
        accountsError.clear();
    } catch (const std::exception& error) {
        accountsError = error.what();
    }
}

gwl::Json AppState::unlock(const std::string& password, bool remember) {
    const gwl::PasswordHash hash = gwl::hashPassword(password);
    try {
        auto opened = gwl::openAccounts(kAccountsFile, hash);
        if (!opened) {
            return "Incorrect master password.";
        }
        if (remember) {
            gwl::rememberPassword(hash);
        }
        accounts = std::move(opened->accounts);
        accountsPassword = opened->password;
        accountsLocked = false;
        accountsError.clear();
        return nullptr;
    } catch (const std::exception& error) {
        return std::string("Could not read Accounts.json: ") + error.what();
    }
}

gwl::Json AppState::setMasterPassword(const std::string& password) {
    if (accountsReloading) return "Accounts.json is being reloaded. Try again when loading finishes.";
    if (!accountsError.empty()) {
        return "Could not save: " + accountsError;
    }
    const std::optional<gwl::PasswordHash> next = password.empty() ? std::nullopt : std::optional<gwl::PasswordHash>(gwl::hashPassword(password));
    try {
        gwl::saveAccounts(accounts, kAccountsFile, next);
    } catch (const std::exception& error) {
        return std::string("Could not save: ") + error.what();
    }
    accountsPassword = next;
    // A remembered password follows the change, so the next start still opens without asking.
    if (gwl::rememberedPassword()) {
        if (next) {
            gwl::rememberPassword(*next);
        } else {
            gwl::forgetPassword();
        }
    }
    return nullptr;
}

void AppState::saveSettings() {
    try {
        settings.save(kSettingsFile);
    } catch (const std::exception& error) {
        std::fprintf(stderr, "could not save settings: %s\n", error.what());
    }
}

gwl::Json AppState::changeAccounts(const std::function<void()>& change) {
    if (accountsReloading) return "Accounts.json is being reloaded. Try again when loading finishes.";
    if (!accountsError.empty()) {
        return "Could not save: " + accountsError;
    }
    try {
        change();
        gwl::saveAccounts(accounts, kAccountsFile, accountsPassword);
        // The list shows the saved accounts, so it reads them again, whichever window made the change.
        if (list) {
            list->emit("refresh", gwl::Json());
        }
        return nullptr;
    } catch (const std::exception& error) {
        return std::string("Could not save: ") + error.what();
    }
}

// The page's startup state. Every window's page asks for it before it draws.
gwl::Json AppState::state() {
    if (!fixedState_) {
        fixedState_ = gwl::Json{
            {"hasLinuxPaths", gwl::launchesThroughWine()},
            {"hasTray", gwl::hasTray()},
            {"version", "GW Launcher r" + std::to_string(gwl::currentBuild()) + " \u00b7 " + gwl::osDescription()},
        };
    }
    if (!installState_) {
        installState_ = gwl::Json{
            {"canLaunchDefaultClient", gwl::defaultInstall().has_value()},
            {"detectedPaths", gwl::Json{{"steam", gwl::detectedSteamPath()}, {"wine", gwl::detectedWinePath()}}},
        };
    }
    const gwl::Settings effective = settings.effective(gwl::hasTray());
    gwl::Json state = {
        {"locked", accountsLocked},
        {"startup", nullptr},
        {"canCreateShortcuts", true},
        {"debug", false},
        {"hasWindowsOptions", true},
        {"hasSetup", false},
        {"setupNeeded", false},
        {"update", availableUpdate ? gwl::Json{{"revision", availableUpdate->revision}} : gwl::Json(nullptr)},
        {"settings", effective.forPage()},
    };
    state.update(*fixedState_);
    state.update(*installState_);
    return state;
}

void AppState::forgetInstallState() {
    installState_.reset();
}

namespace {

// The account at the index the page sent, or null when there's no such account.
const gwl::Account* accountAt(const AppState& app, const gwl::Json& index) {
    if (!index.is_number_unsigned() || index.get<std::size_t>() >= app.accounts.size()) {
        return nullptr;
    }
    return &app.accounts[index.get<std::size_t>()];
}

}  // namespace

std::string accountIconUrl(const gwl::Account& account) {
    if (account.isSteamAccount) {
        if (const auto url = gwl::steamIconUrl()) {
            return *url;
        }
        return helmetIcon();
    }
    return helmetIcon();
}

namespace {

void refreshAccountStates(AppState& app) {
    {
        std::lock_guard<std::mutex> lock(accountListCache.mutex);
        const auto now = std::chrono::steady_clock::now();
        if (accountListCache.pending || now - accountListCache.started < std::chrono::seconds(2)) return;
        accountListCache.pending = true;
        accountListCache.started = now;
    }
    try {
        app.workers.submit([] {
            std::string warning = gwl::captureError([&] {
                auto running = gwl::runningGamePaths();
                std::lock_guard<std::mutex> lock(accountListCache.mutex);
                accountListCache.running = std::move(running);
            }, "Could not refresh game status: ");
            std::lock_guard<std::mutex> lock(accountListCache.mutex);
            accountListCache.warning = std::move(warning);
            accountListCache.pending = false;
        });
    } catch (...) {
        std::lock_guard<std::mutex> lock(accountListCache.mutex);
        accountListCache.pending = false;
        throw;
    }
}

gwl::Json accountList(AppState& app) {
    fetchSteamIconFor(app.accounts, [] {});
    refreshAccountStates(app);
    std::lock_guard<std::mutex> lock(accountListCache.mutex);
    gwl::Json entries = gwl::Json::array();
    for (std::size_t index = 0; index < app.accounts.size(); ++index) {
        const auto& account = app.accounts[index];
        auto entry = account.listRow(accountIconUrl(account));
        const auto key = account.pathKey();
        if (app.launching.count(account.name())) {
            entry["state"] = "Launching";
        } else if (!key || !accountListCache.running.count(*key)) {
            entry["state"] = "Inactive";
        } else {
            const auto started = app.startedBy.find(*key);
            entry["state"] = started != app.startedBy.end() && started->second == index ? "Active" : "In Use";
        }
        entries.push_back(std::move(entry));
    }
    std::string warning = accountListCache.warning;
    if (accountListCache.pending && std::chrono::steady_clock::now() - accountListCache.started > std::chrono::seconds(15)) {
        warning = "Game status is taking longer than expected. Accounts remain available; status may be out of date.";
    }
    if (accountsReloading) warning = "Accounts.json is being reloaded. Changes are disabled until loading finishes.";
    return {{"entries", std::move(entries)}, {"warning", warning.empty() ? gwl::Json(nullptr) : gwl::Json(warning)},
            {"error", app.accountsError.empty() ? gwl::Json(nullptr) : gwl::Json(app.accountsError)}};
}

}  // namespace

void installCommonHandlers(gwl::MessageHandler& handler, AppState& app) {
    handler.on("state", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("state");
        reply.result(app.state());
    });
    handler.on("icon", [](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("icon");
        reply.result(helmetIcon());
    });
    handler.on("accounts", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("accounts");
        reply.result(accountList(app));
    });
    handler.on("pickFile", [](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("pickFile");
        const bool modsOnly = args.at(1).get<bool>();
        const std::string gamePath = args.size() > 2 && args.at(2).is_string() ? args.at(2).get<std::string>() : std::string();
        const auto picked = gwl::pickFile(args.at(0).get<std::string>(), modsOnly, pickerStartFolder(modsOnly, gamePath));
        reply.result(picked ? gwl::Json(*picked) : gwl::Json(nullptr));
    });
    handler.on("pluginFiles", [](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("pluginFiles");
        reply.result(gwl::pluginFilesJson(args.at(0), std::filesystem::current_path()));
    });
    handler.on("openWindow", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("openWindow");
        const std::string view = args.at(1).get<std::string>();
        if (view == "editor") {
            app.openEditor(args.at(0).get<std::string>(), args.at(2));
        } else if (view == "issues") {
            // The page shows the issues window with one argument, the account's view, so the array holds it at 0.
            app.openIssues(args.at(0).get<std::string>(), args.at(2).at(0));
        } else if (view == "settings") {
            app.openSettings();
        } else if (view == "setup") {
            app.openSetup();
        } else {
            reply.error("Unknown window.");
            return;
        }
        reply.result(nullptr);
    });
    handler.on("launch", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("launch");
        app.launch(reply, args, args.at(1).get<bool>(), true);
    });
    handler.on("launchNow", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("launchNow");
        app.launch(reply, args, false, false);
    });
    handler.on("launchDefault", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("launchDefault");
        app.launchDefault(reply);
    });
    handler.on("openMenu", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("openMenu");
        app.openMenu(args.at(0));
        reply.result(nullptr);
    });
    handler.on("copy", [](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("copy");
        gwl::copyText(args.at(0).is_string() ? args.at(0).get<std::string>() : std::string());
        reply.result(nullptr);
    });
    handler.on("openUrl", [](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("openUrl");
        // Only web links are opened, as in the C# launcher.
        const std::string url = args.at(0).is_string() ? args.at(0).get<std::string>() : std::string();
        if (url.rfind("https://", 0) == 0 || url.rfind("http://", 0) == 0) {
            gwl::openUrl(url);
        }
        reply.result(nullptr);
    });
    handler.on("reloadAccounts", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("reloadAccounts");
        // A locked file is read again once it's unlocked, so there's nothing to reload.
        if (app.accountsLocked) {
            reply.result("Accounts.json is locked. Unlock it before reloading.");
            return;
        }
        if (accountsReloading) {
            reply.result("Accounts.json is already being reloaded.");
            return;
        }
        accountsReloading = true;
        const auto alive = std::weak_ptr<bool>(app.alive);
        try {
            app.workers.submit([&app, alive, password = app.accountsPassword, reply] {
                std::optional<gwl::OpenedAccounts> opened;
                auto failure = gwl::captureError([&] { opened = gwl::openAccounts(kAccountsFile, password); }, "Could not reload Accounts.json: ");
                if (failure.empty() && !opened) failure = "Could not reload Accounts.json: the file is locked or its password changed.";
                gwl::runOnUiThread([&app, alive, reply, opened = std::move(opened), failure] {
                    if (alive.expired()) return;
                    accountsReloading = false;
                    app.accountsError = failure;
                    if (opened) {
                        app.accounts = opened->accounts;
                        app.accountsPassword = opened->password;
                        app.startedBy.clear();
                    }
                    reply.result(failure.empty() ? gwl::Json(nullptr) : gwl::Json(failure));
                });
            });
        } catch (...) {
            accountsReloading = false;
            throw;
        }
    });
    handler.on("revealAccount", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("revealAccount");
        const gwl::Account* account = accountAt(app, args.at(0));
        if (!account) {
            reply.result("That account isn't in the list.");
            return;
        }
        const auto gwExe = gwl::linuxPathFor(account->gwpath);
        if (!gwExe) {
            reply.result("Gw.exe's folder couldn't be found on this computer.");
            return;
        }
        gwl::revealInFileExplorer(*gwExe);
        reply.result(nullptr);
    });
    handler.on("shortcut", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("shortcut");
        const gwl::Account* account = accountAt(app, args.at(0));
        if (!account) {
            reply.result("That account isn't in the list.");
            return;
        }
        const auto desktop = gwl::desktopFolder();
        if (!desktop) {
            reply.result("Couldn't find the desktop folder.");
            return;
        }
        // Slashes can't be in a file name, so they become underscores, as the Rust launcher names the shortcut.
        std::string name = account->name();
        for (char& c : name) {
            if (c == '/') {
                c = '_';
            }
        }
        try {
            gwl::createShortcut(*desktop / gwl::pathFromUtf8(name + ".lnk"), gwl::executablePath(), "-launch \"" + name + "\"",
                                "GW Launcher shortcut for " + name, gwl::embeddedHelmetPng());
            reply.result(nullptr);
        } catch (const std::exception& error) {
            reply.result(std::string(error.what()));
        }
    });
    handler.on("saveSettings", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("saveSettings");
        try {
            const gwl::Settings updated = app.settings.applyFields(args.at(0));
            if (const auto problem = gwl::invalidInstallPath(updated.steamPath, updated.winePath)) {
                reply.result(*problem);
                return;
            }
            updated.save(kSettingsFile);
            app.settings = updated;
            gwl::setInstallPaths(updated.steamPath, updated.winePath);
            app.forgetInstallState();
            gwl::setUiScale(static_cast<int>(updated.uiScale));
            if (app.list && app.list->isVisible()) {
                app.list->resize(static_cast<int>(updated.listWidth), static_cast<int>(updated.listHeight));
            }
            if (app.list) {
                app.list->emit("settings", updated.forPage());
            }
            app.applyTrayMode();
            reply.result(nullptr);
        } catch (const std::exception& error) {
            reply.result(std::string(error.what()));
        }
    });
    handler.on("encrypted", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("encrypted");
        reply.result(app.accountsPassword.has_value() || app.accountsLocked);
    });
    handler.on("unlock", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("unlock");
        const gwl::Json message = app.unlock(args.at(0).get<std::string>(), args.at(1).get<bool>());
        reply.result(message);
        if (message.is_null() && app.quickAccount) {
            app.launchNamed(*std::exchange(app.quickAccount, std::nullopt));
        }
    });
    handler.on("setMasterPassword", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("setMasterPassword");
        reply.result(app.setMasterPassword(args.at(0).get<std::string>()));
    });
    handler.on("choose", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("choose");
        std::vector<std::string> options;
        for (const auto& option : args.at(2)) {
            options.push_back(option.get<std::string>());
        }
        app.openChoice(args.at(0).get<std::string>(), args.at(1).get<std::string>(), options,
                       [reply](std::optional<std::size_t> picked) {
                           reply.result(picked ? gwl::Json(*picked) : gwl::Json(nullptr));
                       });
    });
    handler.on("message", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("message");
        app.openMessage(args.at(0).get<std::string>(), args.at(1).get<std::string>());
        reply.result(nullptr);
    });
    handler.on("answer", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("answer");
        std::optional<std::size_t> picked;
        if (args.at(1).is_number_unsigned()) {
            picked = args.at(1).get<std::size_t>();
        }
        app.answerChoice(args.at(0).get<std::uint64_t>(), picked);
        reply.result(nullptr);
    });
    // Progress popups can be cancelled. The running job checks the flag between chunks.
    handler.on("continueLaunch", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("continueLaunch");
        if (app.launchContinue) {
            *app.launchContinue = true;
        }
        reply.result(nullptr);
    });
    handler.on("cancel", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("cancel");
        app.cancelProgress();
        reply.result(nullptr);
    });
    handler.on("showUpdate", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("showUpdate");
        if (app.availableUpdate) {
            app.openLauncherUpdatePrompt(*app.availableUpdate);
        }
        reply.result(nullptr);
    });
    handler.on("refreshMod", [](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("refreshMod");
        reply.result(gwl::refreshedMod(args.at(0)));
    });
    // Resolving a mod does nothing yet, as in the Rust launcher, which never offers the action.
    handler.on("resolveMod", [](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("resolveMod");
        reply.result(nullptr);
    });
    handler.on("setup", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("setup");
        reply.result(app.setupItems());
    });
    handler.on("setupStatus", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("setupStatus");
        reply.result(app.setupStatus());
    });
    handler.on("startSetup", [&app](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("startSetup");
        const bool given = args.size() > 0 && args.at(0).is_string();
        reply.result(app.startSetup(given ? args.at(0).get<std::string>() : std::string()));
    });
    handler.on("updateAll", [&app](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("updateAll");
        app.startUpdateAll(reply);
    });
    handler.on("hide", [&app](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("hide");
        window.close();
        reply.result(nullptr);
        app.endQuickIfIdle();
    });
    // The list answers quit itself. Without it, the password prompt of a quick launch quits.
    handler.on("quit", [](gwl::WebViewWindow&, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("quit");
        gwl::stopEventLoop();
        reply.result(nullptr);
    });
    handler.on("uiScale", [](gwl::WebViewWindow&, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("uiScale");
        gwl::setUiScale(args.at(0).get<int>());
        reply.result(nullptr);
    });
    handler.on("fitToPage", [](gwl::WebViewWindow& window, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("fitToPage");
        window.fitToPage(args.at(0).get<int>(), args.at(1).get<int>());
        reply.result(nullptr);
    });
    handler.on("resize", [](gwl::WebViewWindow& window, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("resize");
        window.resize(args.at(0).get<int>(), args.at(1).get<int>());
        reply.result(nullptr);
    });

    // The page's title bar and edges: every window is frameless, so these stand in for the window manager's.
    handler.on("chrome", [](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("chrome");
        reply.result(window.chrome());
    });
    handler.on("moveDrag", [](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("moveDrag");
        window.beginMove();
        reply.result(nullptr);
    });
    handler.on("resizeDrag", [](gwl::WebViewWindow& window, const gwl::Json& args, const gwl::Reply& reply) {
        logRequest("resizeDrag");
        const int edge = args.at(0).get<int>();
        if (edge >= 0 && edge <= static_cast<int>(gwl::ResizeEdge::SouthEast)) {
            window.beginResize(static_cast<gwl::ResizeEdge>(edge));
        }
        reply.result(nullptr);
    });
    handler.on("minimize", [](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("minimize");
        window.minimize();
        reply.result(nullptr);
    });
    handler.on("toggleMaximize", [](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("toggleMaximize");
        window.toggleMaximize();
        reply.result(nullptr);
    });
    handler.on("close", [](gwl::WebViewWindow& window, const gwl::Json&, const gwl::Reply& reply) {
        logRequest("close");
        window.close();
        reply.result(nullptr);
    });
}

std::string developmentUrl() {
    if (const char* url = std::getenv("GWL_DEV_URL")) {
        return url;
    }
    return {};
}

const std::string& helmetIcon() {
    static const std::string icon = gwl::pngDataUrl(gwl::embeddedHelmetPng());
    return icon;
}

void logRequest(const char* method) {
    std::fprintf(stderr, "page called %s\n", method);
}

void AppState::instanceRequest(const gwl::InstanceRequest& request) {
    if (request.kind == gwl::InstanceRequest::Kind::Launch) {
        launchNamed(request.name);
        return;
    }
    // In tray mode every start puts the list beside the tray, however it was opened. showList restores it if it's minimised and brings it
    // to the front, and a list that's hidden or minimised still counts as showing, so the check isn't made here.
    if (tray && list) {
        showListAtStart();
        return;
    }
    if (list) {
        list->present();
    }
}

void AppState::launchNamed(const std::string& name) {
    for (std::size_t index = 0; index < accounts.size(); ++index) {
        if (accounts[index].name() != name) {
            continue;
        }
        // A failed start shows its reason in the progress window, so only blocking issues need a window of their own.
        auto shown = [this, index, name](const gwl::Json& result) {
            if (result.is_object() && result.contains("issues") && !result["issues"].is_null()) {
                const gwl::Json view{{"index", index}, {"name", name}, {"issues", result["issues"]}, {"canLaunch", result["canLaunch"]}, {"launchBlocked", true}};
                openIssues("launch:" + name, view);
            }
            endQuickIfIdle();
        };
        launch(gwl::Reply{shown, [this](const std::string&) { endQuickIfIdle(); }}, gwl::Json::array({index}), false, true);
        return;
    }
    openMessage("GW Launcher", "There's no account named " + name + ".");
}

std::string AppState::pageHtml() const {
    return gwl::pageWithOverrides(page, gwl::overrideCss(std::filesystem::current_path(), gwl::embeddedOverrideTemplate()));
}

}  // namespace launcher
