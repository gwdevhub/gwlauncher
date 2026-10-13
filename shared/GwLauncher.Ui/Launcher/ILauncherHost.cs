using Avalonia.Controls;
using GW_Launcher.Classes;
using GW_Launcher.Launcher;

namespace GwLauncher.Ui;

// What the shared windows need from a platform. Each host implements it over its own accounts and game launch.
public interface ILauncherHost
{
    bool HasTray { get; }
    bool HasLinuxPaths { get; }
    bool CanCreateShortcuts { get; }
    bool HasSetup { get; }
    bool SetupNeeded { get; }

    // True while encrypted accounts are waiting for the master password.
    bool Locked { get; }
    GlobalSettings Settings { get; }

    // Writes the settings object returned by Settings.
    void SaveSettings();

    // Null when the password opened the accounts. Otherwise the reason it didn't.
    string? Unlock(string password, bool remember);
    void ShowSetup(Window? owner);

    // Starts background work such as update checks. Called once the window system is up.
    void StartBackground();

    // Changes when the tray menu should be rebuilt, such as accounts or setup changing.
    string MenuState();

    // The tray click position, when the platform can report it.
    bool TryTrayAnchor(out int x, out int y);

    IReadOnlyList<AccountEntry> Accounts(out string? error);
    AccountDraft Draft(int? index);

    // Null when saved. Otherwise the reason, for the editor to show.
    string? Save(int? index, AccountDraft draft);
    string? Remove(int index);
    string? Move(int from, int to);

    Task<IReadOnlyList<AccountIssue>> VersionIssues(int index);
    Task<string?> Fix(Window? owner, int index, AccountIssue issue);
    string? Launch(int index);
    string? CreateShortcut(int index);

    // Updates a mod's problem, locate and resolve flags from its current path.
    void RefreshMod(ModDraft mod);
    IReadOnlyList<ModDraft> PluginFiles(AccountDraft draft);
    Task<string?> Resolve(Window? owner, ModDraft mod);
}
