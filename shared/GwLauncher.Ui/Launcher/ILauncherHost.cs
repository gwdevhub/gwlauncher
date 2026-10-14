namespace GwLauncher.Ui;

// The platform's accounts and game launch, as the page needs them. The Linux host runs games through Wine, and the
// Windows host through the multiclient patch. Neither shows UI: anything that asks the user goes through Prompts.
public interface ILauncherHost
{
    bool HasLinuxPaths { get; }
    bool CanCreateShortcuts { get; }
    bool HasWindowsOptions { get; }
    bool CanLaunchDefaultClient { get; }

    // True while encrypted accounts are waiting for the master password.
    bool Locked { get; }
    GlobalSettings Settings { get; }

    // Set by the bridge, so the host can ask questions and show progress on the page.
    IPrompts Prompts { get; set; }

    void SaveSettings();

    // Null when the password opened the accounts. Otherwise the reason it didn't.
    string? Unlock(string password, bool remember);

    // True when Accounts.json is protected with a master password.
    bool Encrypted { get; }

    // Protects Accounts.json with the password, or removes the protection when it's empty. Null when saved.
    string? SetMasterPassword(string password);

    // Starts background work such as update checks. Called once the window is up.
    void StartBackground();

    // Things the platform has to install before accounts can run. Empty when there are none.
    IReadOnlyList<SetupItem> SetupItems();
    // The folder comes from the setup page. Null means the step's suggested folder.
    string? StartSetup(string? folder);
    SetupProgress SetupStatus();

    IReadOnlyList<AccountEntry> Accounts(out string? error);

    // Re-reads the accounts from disk. Returns an error to show, or null.
    string? ReloadAccounts();

    // Starts the default Guild Wars install with no account. Returns an error to show, or null.
    Task<string?> LaunchDefaultClient();

    // Hosts that can't see their game processes return false, so only Launching shows for them.
    bool IsRunning(int index);
    AccountDraft Draft(int? index);

    // Null when saved. Otherwise the reason, for the editor to show.
    string? Save(int? index, AccountDraft draft);
    string? Remove(int index);
    string? Move(int from, int to);

    Task<IReadOnlyList<AccountIssue>> VersionIssues(int index);

    // Runs the fix an issue offers. Returns a status message, or null when nothing changed.
    Task<string?> Fix(int index, AccountIssue issue);
    // A suspended start holds the game until the user confirms it.
    Task<string?> Launch(int index, bool suspended);

    // Updates each distinct Gw.exe behind one progress view. Returns the status line for the list.
    Task<string> UpdateAllClients();
    string? CreateShortcut(int index);

    // Updates a mod's problem, locate and resolve flags from its current path.
    void RefreshMod(ModDraft mod);
    IReadOnlyList<ModDraft> PluginFiles(AccountDraft draft);
    Task<string?> Resolve(ModDraft mod);
}

// Folder is the suggested place to install to, when the step asks for one.
public sealed record SetupItem(string Title, string Description, bool Done, bool CanInstall, string? Folder = null);

public sealed record SetupProgress(bool Running, string LastLine, string LogPath, string? Error = null);
