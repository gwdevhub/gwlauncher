using System.Text.RegularExpressions;
using GW_Launcher.Classes;
using GW_Launcher.Launcher;
using GW_Launcher.Utilities;
using GwLauncher.Linux;
using GwLauncher.Resolve;
using GwLauncher.Ui;
using Newtonsoft.Json;

namespace GwLauncher.Tray;

// The shared page's view of this computer: Accounts.json through the Linux store, launches through Wine, and the
// Linux-only fixes (gMod.dll, Gw.exe, Windows-compacted files) behind the same issue buttons.
public sealed class LinuxLauncherHost : ILauncherHost
{
    private static readonly Regex WindowsPathPattern = new(@"^[A-Za-z]:\\");
    private readonly GlobalSettings _settings = LoadSettings();

    public bool HasLinuxPaths => true;
    public bool CanCreateShortcuts => HostCapabilities.DesktopIcons;
    public bool HasWindowsOptions => false;
    public bool Locked => false;
    public GlobalSettings Settings => _settings;
    public IPrompts Prompts { get; set; } = null!;

    public void SaveSettings() => AppSettings.Save(_settings);

    // Linux reads the master password from GWLAUNCHER_MASTER_PASSWORD when the accounts are opened, so there's nothing to unlock.
    public string? Unlock(string password, bool remember) => null;

    public void StartBackground()
    {
        // Like the Windows launcher, refresh gMod.dll in the background when updates are on. A failure is reported by a launch that needs it.
        if (!_settings.CheckForUpdates)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await GModProvider.RefreshAsync();
            }
            catch (GModUnavailableException)
            {
            }
        });
    }

    public IReadOnlyList<SetupItem> SetupItems() =>
        Setup.Steps.Select(step => new SetupItem(step.Title, step.Description, step.IsSatisfied(), step.Command() != null)).ToList();

    // Starts the first unfinished step. It runs detached, so it keeps going if the window or the launcher closes.
    public string? StartSetup()
    {
        var step = Setup.Steps.FirstOrDefault(s => !s.IsSatisfied());
        var command = step?.Command();
        if (step == null || command == null)
        {
            return null;
        }

        try
        {
            SetupRunner.Start(command, step.NeedsRoot);
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return $"Could not start the {step.Title} install: {ex.Message}";
        }
    }

    public SetupProgress SetupStatus() => new(SetupRunner.InProgress(), SetupRunner.LastLogLine(), SetupRunner.LogPath);

    public IReadOnlyList<AccountEntry> Accounts(out string? error)
    {
        try
        {
            var accounts = OpenStore().Accounts;
            error = null;
            return accounts.Select(account => new AccountEntry(account.Name, AccountDiagnostics.Check(account))).ToList();
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException)
        {
            error = $"Error: {ex.Message}";
            return new List<AccountEntry>();
        }
    }

    public AccountDraft Draft(int? index)
    {
        var account = index is { } i ? OpenStore().Accounts[i] : new LinuxAccount();
        return new AccountDraft
        {
            Title = account.Title,
            Email = account.Email,
            Password = account.Password,
            Character = account.Character,
            GwPath = account.GwPath,
            LinuxGwPath = account.LinuxGwPath ?? "",
            ExtraArgs = account.ExtraArgs,
            UsePluginFolderMods = account.UsePluginFolderMods,
            Mods = account.Mods.Select(mod => ToDraft(mod)).ToList()
        };
    }

    // Checks the fields the way the editor always has, then writes the account. Fields this form doesn't edit are kept.
    public string? Save(int? index, AccountDraft draft)
    {
        var winPath = draft.GwPath.Trim();
        var linuxPath = draft.LinuxGwPath.Trim();

        if (string.IsNullOrWhiteSpace(draft.Email))
        {
            return "Enter the account's email.";
        }

        if (winPath == "" && linuxPath == "")
        {
            return "Enter a path to Gw.exe.";
        }

        if (linuxPath != "" && !FileCheck.IsUsableFile(linuxPath))
        {
            return "Gw.exe was not found at the Linux path.";
        }

        // With no Windows path, the Linux path is the account's path, as on a Linux-only account.
        var gwPath = winPath == "" ? linuxPath : winPath;
        var linuxGwPath = winPath == "" || linuxPath == "" ? null : linuxPath;

        if (linuxGwPath == null && !WindowsPathPattern.IsMatch(gwPath) && !FileCheck.IsUsableFile(gwPath))
        {
            return "Gw.exe was not found at that path.";
        }

        var store = OpenStore();
        var existing = index is { } i ? store.Accounts[i] : null;
        var account = new LinuxAccount
        {
            Title = draft.Title.Trim(),
            Email = draft.Email.Trim(),
            Password = draft.Password,
            Character = draft.Character.Trim(),
            GwPath = gwPath,
            LinuxGwPath = linuxGwPath,
            ExtraArgs = draft.ExtraArgs.Trim(),
            Mods = draft.Mods.Select(ToMod).ToList(),
            UsePluginFolderMods = draft.UsePluginFolderMods,
            Extra = existing?.Extra ?? new Dictionary<string, Newtonsoft.Json.Linq.JToken>()
        };

        if (index is { } position)
        {
            store.Replace(position, account);
        }
        else
        {
            store.Add(account);
        }

        return SaveStore(store);
    }

    public string? Remove(int index)
    {
        var store = OpenStore();
        store.Remove(index);
        return SaveStore(store);
    }

    public string? Move(int from, int to)
    {
        var store = OpenStore();
        store.Move(from, to);
        return SaveStore(store);
    }

    public async Task<IReadOnlyList<AccountIssue>> VersionIssues(int index) =>
        await AccountDiagnostics.CheckGwExeVersionAsync(OpenStore().Accounts[index]);

    // Runs the fix for one issue and returns a status message, or null when nothing changed.
    public async Task<string?> Fix(int index, AccountIssue issue)
    {
        if (issue.WofPath != null)
        {
            var resolved = await ResolveAndRepoint(index, issue.WofPath);
            return resolved == "" ? null : $"Decompressed to {resolved}. Reopen the account to check its issues again.";
        }

        if (issue.CanDownloadGMod)
        {
            var path = await Prompts.Progress<string>(
                new ProgressOptions(
                    "Downloading gMod.dll",
                    "Downloading gMod.dll from GitHub",
                    "Texmods need it. It's saved next to the launcher.",
                    FailureText: "gMod.dll couldn't be downloaded"),
                (cancellation, progress) => GModProvider.RefreshAsync(progress, cancellation));
            return path == null ? null : $"gMod.dll saved to {path}. Reopen the account to check its issues again.";
        }

        if (issue.CanUpdateGwExe)
        {
            var gwPath = OpenStore().Accounts[index].EffectiveGwPath;
            var updated = await Prompts.Progress<string>(
                new ProgressOptions(
                    "Updating Gw.exe",
                    "Downloading the latest Gw.exe from ArenaNet",
                    "It replaces the account's Gw.exe. Gw.dat is left as it is.",
                    FailureText: "Gw.exe couldn't be updated"),
                async (cancellation, progress) =>
                {
                    await GwExeUpdater.UpdateAsync(gwPath, progress, cancellation);
                    return gwPath;
                });
            return updated == null ? null : $"Gw.exe updated at {gwPath}.";
        }

        return null;
    }

    public Task<string?> Launch(int index) => Task.FromResult(StartNow(index));

    public string? StartNow(int index)
    {
        try
        {
            var account = OpenStore().Accounts[index];
            var psi = WineLauncher.BuildStartInfo(account);
            WineLauncher.PrepareLaunch(account);
            WineLauncher.Start(psi);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public string? CreateShortcut(int index)
    {
        try
        {
            var launcher = Environment.ProcessPath
                ?? throw new InvalidOperationException("The launcher's location isn't known, so no shortcut can be made.");
            WebAssets.SaveIcon(AppPaths.IconFile);
            DesktopShortcut.Create(OpenStore().Accounts[index], launcher);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ex.Message;
        }
    }

    public void RefreshMod(ModDraft mod)
    {
        var linux = FromDraft(mod);
        mod.CanLocate = linux.IsWindowsPath || linux.LinuxPath != null;
        mod.Problem = linux.IsWindowsPath
            ? "Windows path with no Linux path. Use Locate to choose the file here."
            : ProblemFor(linux.EffectivePath);
        mod.CanResolve = mod.Problem != "" && ReparseInspector.IsWofStub(linux.EffectivePath);
    }

    public IReadOnlyList<ModDraft> PluginFiles(AccountDraft draft)
    {
        var gwHint = (draft.LinuxGwPath.Trim() is { Length: > 0 } linux ? linux : draft.GwPath.Trim());
        return LinuxMod.PluginFolderFiles(gwHint).Select(plugin =>
        {
            var problem = plugin.TargetPath == null
                ? "Shortcut target couldn't be found on this computer"
                : ProblemFor(plugin.TargetPath);
            var path = plugin.TargetPath ?? plugin.SourcePath;
            return new ModDraft
            {
                FileName = plugin.SourcePath,
                Type = plugin.Type,
                Target = plugin.TargetPath,
                ReadOnly = true,
                Problem = problem,
                CanResolve = problem != "" && ReparseInspector.IsWofStub(path)
            };
        }).ToList();
    }

    public Task<string?> Resolve(ModDraft mod) =>
        ResolveFile(mod.ReadOnly ? mod.Target ?? mod.FileName : (mod.LinuxPath is { Length: > 0 } linux ? linux : mod.FileName));

    // The account's index in Accounts.json, for the shortcut launch path.
    public int IndexOf(string name)
    {
        return OpenStore().Accounts.FindIndex(account => account.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    // Opens Accounts.json for one operation. Every call reads the file again, so the file stays the source of truth.
    public static AccountStore OpenStore()
    {
        var store = new AccountStore(AppPaths.AccountsFile);
        store.Load(() => Environment.GetEnvironmentVariable("GWLAUNCHER_MASTER_PASSWORD")
            ?? throw new InvalidOperationException("Encrypted accounts need GWLAUNCHER_MASTER_PASSWORD set."));
        return store;
    }

    private async Task<string> ResolveAndRepoint(int index, string path)
    {
        var resolved = await ResolveFile(path);
        if (resolved == null || resolved == path)
        {
            return resolved ?? "";
        }

        var store = OpenStore();
        var (changed, text) = AccountRepoint.Apply(store.Accounts[index], path, resolved);
        if (changed)
        {
            store.Save();
        }

        return resolved + ". " + text;
    }

    // Decompresses a Windows-compacted file, after asking what to do with the compressed original. Null when cancelled or failed.
    private async Task<string?> ResolveFile(string path)
    {
        var name = Path.GetFileName(path);
        var choice = await Prompts.Choose(
            $"Decompress {name}",
            $"{name} is compressed by Windows, so Linux can't read it.\n\n" +
            "Keep the original: it moves into a \"compressed\" folder beside the file, and the plain copy takes its place.\n" +
            "Replace the original: the plain copy is verified first, then takes its place, and the compressed file is removed.",
            "Cancel", "Keep the original", "Replace the original");
        if (choice is not (1 or 2))
        {
            return null;
        }

        var mode = choice == 2 ? ResolveMode.ReplaceOriginal : ResolveMode.DecompressNextToOriginal;
        return await Prompts.Progress<string>(
            new ProgressOptions(
                "Decompressing…",
                $"Decompressing {name}",
                mode == ResolveMode.ReplaceOriginal
                    ? "If asked, enter your password in the system prompt. The original is replaced once the copy is verified. This can take several minutes. Cancelling removes the partial copy."
                    : "If asked, enter your password in the system prompt. The original is kept. This can take several minutes. Cancelling removes the partial copy.",
                FailureText: "The file could not be decompressed"),
            (cancellation, progress) => WofResolver.ResolveAsync(
                path,
                mode,
                new Progress<ResolveProgress>(p => progress.Report(new ProgressUpdate(p.Fraction, p.Detail))),
                cancellation));
    }

    private static string? SaveStore(AccountStore store)
    {
        try
        {
            store.Save();
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Could not save: {ex.Message}";
        }
    }

    private static GlobalSettings LoadSettings()
    {
        try
        {
            return AppSettings.Load();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // An unreadable settings file shouldn't stop the launcher; it runs on defaults until the next save.
            return new GlobalSettings();
        }
    }

    private static ModDraft ToDraft(LinuxMod mod)
    {
        var draft = new ModDraft
        {
            FileName = mod.FileName,
            Type = mod.Type,
            Active = mod.Active,
            LinuxPath = mod.LinuxPath ?? ""
        };
        return draft;
    }

    private static LinuxMod ToMod(ModDraft draft) => new()
    {
        FileName = draft.FileName,
        Type = draft.Type,
        Active = draft.Active,
        LinuxPath = draft.LinuxPath is { Length: > 0 } path ? path : null
    };

    private static LinuxMod FromDraft(ModDraft draft) => ToMod(draft);

    // Problems are judged on the path the mod will load from: its Linux path if one is set.
    private static string ProblemFor(string path)
    {
        if (FileCheck.IsUsableFile(path))
        {
            return "";
        }

        var reason = ReparseInspector.UnusableReason(path);
        var sentence = char.ToUpperInvariant(reason[0]) + reason[1..];
        return ReparseInspector.IsWofStub(path) ? sentence + " Use Resolve to decompress it." : sentence;
    }
}
