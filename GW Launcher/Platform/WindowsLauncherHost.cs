using Avalonia.Controls;
using GwLauncher.Ui;
using GW_Launcher.Guildwars;
using GW_Launcher.Launcher;

namespace GW_Launcher.Platform;

// The shared windows' view of Windows: Accounts.json with the DPAPI-protected master password, launches through the
// multiclient patch, and the update checks that run at startup.
public sealed class WindowsLauncherHost : ILauncherHost
{
    private const string SessionKey = @"SOFTWARE\GW_Launcher\Session";
    private const string CachedPasswordValue = "CachedPassword";
    private readonly AccountManager _store = new("Accounts.json");
    private bool _locked;

    public WindowsLauncherHost()
    {
        var cached = CachedPasswordHash();
        if (!_store.Load(cached))
        {
            if (cached != null)
            {
                ClearCachedPassword();
            }

            _locked = true;
        }
    }

    public bool HasTray => true;
    public bool HasLinuxPaths => false;
    public bool CanCreateShortcuts => true;
    public bool HasSetup => false;
    public bool SetupNeeded => false;
    public bool HasWindowsOptions => true;
    public bool Locked => _locked;
    public GlobalSettings Settings => Program.Settings;

    public void SaveSettings() => Program.Settings.Save();

    public string? Unlock(string password, bool remember)
    {
        var hash = AccountManager.HashPassword(password);
        if (!_store.Load(hash))
        {
            return "Incorrect master password.";
        }

        if (remember)
        {
            CachePasswordHash(hash);
        }

        _locked = false;
        return null;
    }

    public void ShowSetup(Window? owner)
    {
    }

    public void StartBackground()
    {
        WarnIfInGwDirectory();
        if (!Program.Settings.CheckForUpdates)
        {
            return;
        }

        Background(WindowsUpdater.CheckLauncherUpdate);
        Background(WindowsUpdater.RefreshGMod);
        if (!_locked)
        {
            Background(() => WindowsUpdater.CheckGwExeUpdates(_store.ToList()));
        }
    }

    public string MenuState() => File.Exists("Accounts.json")
        ? $"{File.GetLastWriteTimeUtc("Accounts.json").Ticks}:{_store.Length}"
        : "none";

    // The cursor is over the tray icon when its click arrives.
    public bool TryTrayAnchor(out int x, out int y)
    {
        x = y = 0;
        if (!GetCursorPos(out var point))
        {
            return false;
        }

        x = point.X;
        y = point.Y;
        return true;
    }

    public bool TryStoredTrayAnchor(out int x, out int y)
    {
        x = y = 0;
        try
        {
            var parts = File.ReadAllText(TrayAnchorFile).Split(',');
            return parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void RememberTrayAnchor(int x, int y)
    {
        try
        {
            File.WriteAllText(TrayAnchorFile, $"{x},{y}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Startup then falls back to the screen corner, so the failure doesn't need reporting.
        }
    }

    private static string TrayAnchorFile => Path.Combine(AppContext.BaseDirectory, "tray-anchor.txt");

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    public IReadOnlyList<AccountEntry> Accounts(out string? error)
    {
        if (_locked)
        {
            error = "Accounts are locked. Restart GW Launcher and enter the master password.";
            return new List<AccountEntry>();
        }

        error = null;
        return _store.Select(account => new AccountEntry(account.Name, LocalIssues(account))).ToList();
    }

    public AccountDraft Draft(int? index)
    {
        var account = index is { } i ? _store[i] : new Account();
        return new AccountDraft
        {
            Title = account.title,
            Email = account.email,
            Password = account.password,
            Character = account.character,
            GwPath = account.gwpath,
            ExtraArgs = account.extraargs,
            UsePluginFolderMods = account.usePluginFolderMods,
            SteamAccount = account.is_steam_account,
            Elevated = account.elevated,
            Mods = account.mods.Select(mod => ToDraft(mod)).ToList()
        };
    }

    // Steam accounts need no email. Everything else needs one, and Gw.exe must exist at the path.
    public string? Save(int? index, AccountDraft draft)
    {
        var steam = draft.SteamAccount;
        if (!steam && string.IsNullOrWhiteSpace(draft.Email))
        {
            return "Enter the account's email.";
        }

        var path = draft.GwPath.Trim();
        if (path == "")
        {
            return "Enter a path to Gw.exe.";
        }

        if (!File.Exists(path))
        {
            return "Gw.exe was not found at that path.";
        }

        var account = index is { } i ? _store[i] : new Account();
        account.title = draft.Title.Trim();
        account.email = draft.Email.Trim();
        account.password = draft.Password;
        account.character = draft.Character.Trim();
        account.gwpath = path;
        account.extraargs = draft.ExtraArgs.Trim();
        account.usePluginFolderMods = draft.UsePluginFolderMods;
        account.is_steam_account = steam;
        account.elevated = draft.Elevated;
        account.mods = draft.Mods.Select(mod => new Mod { active = mod.Active, fileName = mod.FileName, type = mod.Type }).ToList();

        try
        {
            if (index is { } position)
            {
                _store[position] = account;
            }
            else
            {
                _store.Add(account);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not save: {ex.Message}";
        }

        return null;
    }

    public string? Remove(int index)
    {
        try
        {
            _store.Remove(index);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not save: {ex.Message}";
        }
    }

    public string? Move(int from, int to)
    {
        try
        {
            _store.Move(from, to);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not save: {ex.Message}";
        }
    }

    // The Gw.exe check fails open: a network or parse failure lets the launch go ahead, as the Windows launcher always did.
    public async Task<IReadOnlyList<AccountIssue>> VersionIssues(int index)
    {
        var account = _store[index];
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var (latest, _) = await GuildwarsLatestExe.GetAsync(timeout.Token);
            if (latest != null && WindowsUpdater.IsOutdated(account, latest.Value.FileId))
            {
                return new List<AccountIssue>
                {
                    new(IssueSeverity.Warning, "Gw.exe is out of date. Update it to match the latest game build.", CanUpdateGwExe: true)
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Gw.exe version check failed, launching anyway: {ex.Message}");
        }

        return new List<AccountIssue>();
    }

    public async Task<string?> Fix(Window? owner, int index, AccountIssue issue)
    {
        if (!issue.CanUpdateGwExe)
        {
            return null;
        }

        var account = _store[index];
        var updated = await Dialogs.Progress<string>(
            owner,
            new ProgressOptions(
                "Updating Gw.exe",
                "Downloading the latest Gw.exe from ArenaNet",
                "It replaces the account's Gw.exe. Gw.dat is left as it is.",
                FailureText: "Gw.exe couldn't be updated. If the game is in a protected folder, run GW Launcher as administrator"),
            async (cancellation, progress) =>
            {
                await GwDownloader.UpdateClients(new[] { account },
                    new Progress<(string Stage, double Progress)>(update => progress.Report(new ProgressUpdate(update.Progress, update.Stage))),
                    cancellation);
                return account.gwpath;
            });

        return updated == null ? null : $"Gw.exe updated at {account.gwpath}.";
    }

    // The game start waits on the window for up to half a minute, so it runs off the UI thread.
    public Task<string?> Launch(int index)
    {
        var account = _store[index];
        return Task.Run(() => WindowsGame.Launch(account));
    }

    public string? CreateShortcut(int index)
    {
        var account = _store[index];
        var exe = Environment.ProcessPath!;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), account.Name + ".lnk");
        try
        {
            ShortcutWriter.Create(path, exe, $"-launch \"{account.Name}\"", Path.GetDirectoryName(exe)!, "GW Launcher shortcut for " + account.Name);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return ex.Message;
        }
    }

    public void RefreshMod(ModDraft mod)
    {
        mod.Problem = ModManager.ModFileExists(mod.FileName) ? "" : "File not found";
        mod.CanLocate = false;
        mod.CanResolve = false;
    }

    public IReadOnlyList<ModDraft> PluginFiles(AccountDraft draft)
    {
        var probe = new Account { gwpath = draft.GwPath.Trim(), usePluginFolderMods = true };
        return ModManager.GetPluginFolderMods(probe)
            .Select(plugin => new ModDraft { FileName = plugin.filePath, Type = plugin.type, ReadOnly = true })
            .ToList();
    }

    // Windows has no compressed-file fixes; the Linux host supplies them.
    public Task<string?> Resolve(Window? owner, ModDraft mod) => Task.FromResult<string?>(null);

    // Problems that stop a launch or leave something unloaded, shown before the account is started.
    private static List<AccountIssue> LocalIssues(Account account)
    {
        var issues = new List<AccountIssue>();
        if (!File.Exists(account.gwpath))
        {
            issues.Add(new AccountIssue(IssueSeverity.Blocking, $"Gw.exe not found at {account.gwpath}."));
        }

        if (account.is_steam_account && Process.GetProcessesByName("steam").Length == 0)
        {
            issues.Add(new AccountIssue(IssueSeverity.Blocking, "Steam is not running. Start Steam before launching a Steam account."));
        }

        if (ModManager.FindUnsupportedToolboxPlugin(account) != null)
        {
            issues.Add(new AccountIssue(IssueSeverity.Blocking, "GWToolboxdll.dll is in a plugins folder. Add it to the account's mod list instead."));
        }

        foreach (var mod in account.mods.Where(mod => mod.active && !ModManager.ModFileExists(mod.fileName)))
        {
            issues.Add(new AccountIssue(IssueSeverity.Warning, $"{Path.GetFileName(mod.fileName)} was not found."));
        }

        if (ModManager.GetTexmods(account).Any() && !ModManager.GetDlls(account).Any(ModSelection.IsGMod))
        {
            issues.Add(new AccountIssue(IssueSeverity.Blocking, "Texmods need gMod.dll, which isn't next to the launcher."));
        }

        return issues;
    }

    private ModDraft ToDraft(Mod mod)
    {
        var draft = new ModDraft { FileName = mod.fileName, Type = mod.type, Active = mod.active };
        RefreshMod(draft);
        return draft;
    }

    private static void WarnIfInGwDirectory()
    {
        var exePath = Environment.ProcessPath ?? "";
        var exeDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        var exeLower = exePath.ToLowerInvariant();

        var inProgramFiles = exeLower.Contains(@"program files\") || exeLower.Contains(@"program files (x86)\");
        if (inProgramFiles || File.Exists(Path.Combine(exeDirectory, "gw.exe")))
        {
            Dialogs.Message("Warning",
                "This application should not be run from Program Files or the Guild Wars directory.\n\nPlease move GW Launcher to its own folder in your User folder.");
        }
    }

    // Runs a background check, reporting its failure to the console rather than losing it.
    private static void Background(Func<Task> check) => _ = Task.Run(async () =>
    {
        try
        {
            await check();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Background check failed: {ex.Message}");
        }
    });

    private static byte[]? CachedPasswordHash()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SessionKey);
            if (key?.GetValue(CachedPasswordValue) is string cached)
            {
                return System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(cached), null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            }
        }
        catch (Exception ex) when (ex is IOException or System.Security.Cryptography.CryptographicException or FormatException or UnauthorizedAccessException)
        {
            // An unreadable cache means the user is asked for the password.
        }

        return null;
    }

    private static void CachePasswordHash(byte[] hash)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(SessionKey);
            var protectedHash = System.Security.Cryptography.ProtectedData.Protect(hash, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            key.SetValue(CachedPasswordValue, Convert.ToBase64String(protectedHash), Microsoft.Win32.RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            Console.WriteLine($"Could not remember the master password: {ex.Message}");
        }
    }

    private static void ClearCachedPassword()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(SessionKey, true);
            key?.DeleteValue(CachedPasswordValue, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Could not clear the remembered master password: {ex.Message}");
        }
    }
}
