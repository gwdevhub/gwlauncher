// What the launcher's windows share: the page, the settings, the accounts, the jobs, and the windows opened from the list.

#pragma once

#include <atomic>
#include <chrono>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <map>
#include <memory>
#include <optional>
#include <set>
#include <string>
#include <vector>

#include "Accounts.h"
#include "Geometry.h"
#include "Instance.h"
#include "IssueChecker.h"
#include "LauncherUpdate.h"
#include "Messages.h"
#include "Progress.h"
#include "Settings.h"
#include "tray/Tray.h"
#include "WebViewWindow.h"
#include "WorkerPool.h"

namespace launcher {

class AccountListWindow;

struct AppState {
    // Reads Settings.json and Accounts.json from the folder the launcher runs in.
    explicit AppState(std::string page);

    // The workers for checks and other work that mustn't block the window. Declared first, so it's destroyed last.
    gwl::WorkerPool workers{gwl::kWorkerThreads};

    std::string page;
    gwl::Settings settings;
    std::vector<gwl::Account> accounts;
    std::string accountsError;
    // Accounts.json's password, when the file is encrypted, and whether it's still locked until the password is entered.
    std::optional<gwl::PasswordHash> accountsPassword;
    bool accountsLocked = false;
    // Opens Accounts.json at startup, with the remembered password when the file is encrypted. Defined in AppState.cpp.
    void openAccountsFile();
    // Opens the locked accounts with the password, and remembers it when asked. The reply is the page's message, or null.
    gwl::Json unlock(const std::string& password, bool remember);
    // Sets the password the accounts are encrypted with, or removes the encryption with an empty one. The reply is the page's message.
    gwl::Json setMasterPassword(const std::string& password);

    // The account list, which menu picks are sent to.
    AccountListWindow* list = nullptr;
    // Editors stay open once opened, hidden when closed, so they can be shown again.
    std::map<std::string, std::unique_ptr<gwl::WebViewWindow>> editors;
    // Menus are made each time one opens, and the old ones are closed then.
    std::vector<std::unique_ptr<gwl::WebViewWindow>> menus;

    // Runs a change to the accounts and saves them. Returns null, or the failure the page shows.
    gwl::Json changeAccounts(const std::function<void()>& change);

    // The page's startup state: settings, flags, the lock and any update.
    gwl::Json state() const;
    // The page the windows show: the built page with the user's stylesheet overrides, read each time a window opens.
    std::string pageHtml() const;

    // Checks the account on the workers, against the machine as it is now. done gets the issues on the window's thread.
    void checkAccount(const gwl::Account& account, std::function<void(const gwl::IssueChecker::Issues&)> done);
    // Runs the fix the page sent for the account at index. The reply is the message to show when something changed, or null.
    void fixIssue(std::size_t index, const gwl::Json& fix, gwl::Reply reply);

    // Shows the issue list for an account under key, or opens one with the page's arguments. Defined in IssuesWindow.cpp.
    void openIssues(const std::string& key, const gwl::Json& args);
    // Does what a request from a start asks for: launches the named account, or shows the list.
    void instanceRequest(const gwl::InstanceRequest& request);
    // Launches the account with the name, as a -launch start does. Nothing happens when no account has the name.
    void launchNamed(const std::string& name);
    // Shows the settings window, or opens it. Defined in SettingsWindow.cpp.
    void openSettings();

    // Shows the editor under key, or opens one with the page's arguments. Defined in AccountEditWindow.cpp.
    void openEditor(const std::string& key, const gwl::Json& args);
    // Opens the context menu at the pointer, with the page's items. Defined in ContextMenuWindow.cpp.
    void openMenu(const gwl::Json& items);

    // Prompts. A choice goes to whoever asked: done gets the index picked, or nothing when the window closes unanswered.
    using ChoiceDone = std::function<void(std::optional<std::size_t>)>;
    void openChoice(const std::string& title, const std::string& message, const std::vector<std::string>& options, ChoiceDone done);
    // Opens a prompt with the page's view, of the given size. The prompt's id is set here. Defined in PromptWindow.cpp.
    void openPrompt(const std::string& view, gwl::Json prompt, int width, int height, ChoiceDone done);
    void answerChoice(std::uint64_t id, std::optional<std::size_t> answer);
    // A message window, reused when the same message is shown again. Defined in MessageWindow.cpp.
    void openMessage(const std::string& title, const std::string& text);

    std::uint64_t nextChoice = 1;
    std::map<std::uint64_t, ChoiceDone> pendingChoices;
    std::map<std::uint64_t, std::unique_ptr<gwl::WebViewWindow>> choiceWindows;
    std::map<std::string, std::unique_ptr<gwl::WebViewWindow>> messageWindows;
    std::map<std::string, std::unique_ptr<gwl::WebViewWindow>> issueWindows;
    std::unique_ptr<gwl::WebViewWindow> settingsWindow;

    // Launching. The names of the accounts being launched, and the account that started each running game, by its path.
    std::set<std::string> launching;
    std::map<std::string, std::size_t> startedBy;

    // Starts the account at args[0], as the list's Launch does. With suspended, the game waits for the user's answer. The
    // reply is the page's launch result, or the error text when launchNow or launchDefault fails. Defined in Launching.cpp.
    void launch(gwl::Reply reply, const gwl::Json& args, bool suspended, bool checkIssues);
    void launchDefault(gwl::Reply reply);

    // Starts the game on its own thread. done gets the failure, or nothing, back on the window's thread.
    // Checks the account (when checkFirst is set) and starts it behind one progress window. done gets the failure, or the launch's
    // blocking issues as the page's result when the checks stopped it.
    void startGameAsync(const gwl::Account& account, std::optional<std::size_t> index, bool suspended, bool checkFirst,
                        std::function<void(const std::optional<std::string>& failure, const std::optional<gwl::Json>& blocked)> done);

    // Each account's state in the list: Launching, Active, In Use or Inactive.
    std::vector<std::string> accountStates() const;
    // Keeps which game each account started right when the list changes: removed accounts drop out, and the others move.
    void accountRemoved(std::size_t index);
    void accountMoved(std::size_t from, std::size_t to);

    // The tray icon, present in tray mode. With it up, closing the list hides it instead of quitting.
    std::unique_ptr<gwl::TrayIcon> tray;
    // Where the tray icon last was, from a click on it, so Open from the tray menu shows the list beside it.
    std::optional<gwl::Rect> trayAnchor;
    // When the list was last shown. Focus that leaves the list just after a tray click doesn't hide it.
    std::chrono::steady_clock::time_point listShownAt{};
    // When focus loss last hid the list. The tray click that caused it reaches the tray after the list is already hidden.
    std::chrono::steady_clock::time_point listHiddenByFocusAt{};
    // Adds or removes the tray icon to match the tray setting. Without the icon the list is always shown.
    void applyTrayMode();
    // Adds or removes the icon itself, as applyTrayMode's setting asks.
    void setTrayIcon();
    // Handles a tray click or tray menu pick. Defined in AppState.cpp.
    void trayEvent(gwl::TrayAction action, int x, int y);
    // Shows the list beside the tray icon when there's an anchor, or in the middle of the screen without one.
    void showList(std::optional<gwl::Rect> click, std::optional<gwl::Rect> trayRect = std::nullopt);
    void showListAtStart();
    void hideList();
    // Remembers the list's place and size in the settings, while it's shown.
    void rememberListPlace();
    void saveSettings();

    // Guild Wars setup and Gw.exe updates. The state is only read or written on the window's thread; jobs reach it through runOnUiThread.
    struct SetupState {
        bool running = false;
        std::string line;
        std::optional<std::string> error;
    };
    SetupState setup;
    // The setup item the setup window lists: the default folder, and whether a Guild Wars install is already known.
    gwl::Json setupItems() const;
    // Starts the install into folder, or the default folder when it's blank. The reply is the page's message: an error, or null.
    gwl::Json startSetup(const std::string& folder);
    gwl::Json setupStatus() const;
    // Shows the setup window, or opens it. Defined in SetupWindow.cpp.
    void openSetup();
    std::unique_ptr<gwl::WebViewWindow> setupWindow;

    // Replaces each distinct account Gw.exe with the latest build, with the progress window showing it. The reply is the page's message.
    void startUpdateAll(gwl::Reply reply);
    // Cancels the job that's running, if any. The progress window stays until the page hides it.
    void cancelProgress();
    // Runs work on its own thread behind the progress window, which the popup describes. done gets the failure, or nothing.
    // Defined in Progress.cpp.
    // summary, when given, gives the finished message once the work is done, and that message keeps the window open until Done.
    void runWithProgress(const gwl::Json& popup, const gwl::ProgressWork& work, gwl::ProgressDone done,
                         std::function<std::string()> summary = nullptr);
    // The progress window for the latest job, and the job it shows. Progress for an earlier job is dropped.
    std::unique_ptr<gwl::WebViewWindow> progressWindow;
    std::uint64_t progressJob = 0;
    std::shared_ptr<std::atomic<bool>> progressCancel;
    // Set by the progress window's Continue, for a suspended start that's waiting to resume the game.
    std::shared_ptr<std::atomic<bool>> launchContinue;

    // GW Launcher's own update: the newest release, when it's newer than this build. Defined in AppUpdate.cpp.
    std::optional<gwl::LauncherRelease> availableUpdate;
    // Asks GitHub at startup whether a newer launcher is out. Shows the update icon, and the prompt when the setting asks.
    void checkLauncherUpdate();
    // Asks whether to update to the release. Update downloads and installs it, then this launcher exits.
    void openLauncherUpdatePrompt(const gwl::LauncherRelease& release);
    void startLauncherUpdate(const gwl::LauncherRelease& release);
    // The arguments the launcher was started with, which a restarted launcher is started with too.
    std::vector<std::string> launchArguments;
};

// The handlers every window answers, for the calls the page makes from any of its windows.
void installCommonHandlers(gwl::MessageHandler& handler, AppState& app);

// GWL_DEV_URL loads the page from the Vite dev server, so edits show without a rebuild. Any build honours it.
std::string developmentUrl();

// The icon for a row: the Steam icon for Steam accounts, and the helmet for the rest, as data URLs.
const std::string& helmetIcon();
// The image beside an account, as the account list shows it. Used for the account's launch window.
std::string accountIconUrl(const gwl::Account& account);
const std::string& steamIcon();

void logRequest(const char* method);

}  // namespace launcher
