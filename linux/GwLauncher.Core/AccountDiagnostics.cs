using System.Text.RegularExpressions;

namespace GwLauncher.Linux;

public enum IssueSeverity
{
    // Stops the account from launching.
    Blocking,

    // Doesn't stop the launch, but something won't load.
    Warning
}

// WofPath is set when the issue is a Windows-compacted file that can be resolved by decompressing it.
// CanDownloadGMod is set when the fix is downloading gMod.dll next to the launcher.
// CanUpdateGwExe is set when the fix is downloading the latest Gw.exe over the account's copy.
public sealed record AccountIssue(IssueSeverity Severity, string Message, string? WofPath = null, bool CanDownloadGMod = false, bool CanUpdateGwExe = false);

// Raised when an account has blocking issues. The message lists them, so it can be shown to the user as-is.
public sealed class AccountBlockedException : InvalidOperationException
{
    public AccountBlockedException(string accountName, IReadOnlyList<AccountIssue> issues)
        : base($"'{accountName}' can't be launched:\n" + string.Join("\n", issues.Select(i => "- " + i.Message)))
    {
        Issues = issues;
    }

    public IReadOnlyList<AccountIssue> Issues { get; }
}

// Everything an account needs on disk before the game can start. Checks files as the launch would use them.
public static partial class AccountDiagnostics
{
    private static readonly TimeSpan GwVersionCheckTimeout = TimeSpan.FromSeconds(20);

    public static List<AccountIssue> Check(LinuxAccount account)
    {
        var issues = new List<AccountIssue>();
        var gwPath = account.EffectiveGwPath;

        if (WindowsPathPattern().IsMatch(gwPath))
        {
            issues.Add(Blocking($"Game path is a Windows path with no Linux path set: {gwPath}"));
        }
        else
        {
            CheckFile(issues, gwPath, "Gw.exe");

            var gwFolder = Path.GetDirectoryName(gwPath);
            if (!string.IsNullOrEmpty(gwFolder))
            {
                CheckFile(issues, Path.Combine(gwFolder, "Gw.dat"), "Gw.dat");
            }
        }

        var needsLoader = false;
        foreach (var mod in account.Mods.Where(mod => mod.Active))
        {
            if (mod.IsWindowsPath)
            {
                issues.Add(Blocking($"{ModLabel(mod)} has a Windows path with no Linux path set: {mod.FileName}"));
            }
            else
            {
                CheckFile(issues, mod.EffectivePath, ModLabel(mod));
            }

            // Texmods load through gMod, which is a DLL, so they need the loader too.
            needsLoader = true;
        }

        if (account.UsePluginFolderMods)
        {
            foreach (var plugin in LinuxMod.PluginFolderFiles(gwPath))
            {
                var label = $"Plugin {Path.GetFileName(plugin.SourcePath)}";
                if (plugin.TargetPath == null)
                {
                    issues.Add(new AccountIssue(IssueSeverity.Warning,
                        $"{label}: the shortcut's target couldn't be found on this computer, so it won't load."));
                    continue;
                }

                CheckFile(issues, plugin.TargetPath, label);
                needsLoader = true;
            }
        }

        // Texmods need gMod, and gMod is only added to the launch when it's already usable. Otherwise it's an issue
        // with a fix, rather than something launch quietly downloads.
        var gmod = GModProvider.LocalPath;
        if (WineLauncher.TexmodsToLoad(account).Count > 0
            && !WineLauncher.DllsToLoad(account).Any(WineLauncher.IsGMod)
            && !GModProvider.IsValid(gmod))
        {
            issues.Add(File.Exists(gmod)
                ? new AccountIssue(IssueSeverity.Blocking, $"Texmods need gMod.dll, but the copy at {gmod} isn't a usable 32-bit DLL.", null, true)
                : new AccountIssue(IssueSeverity.Blocking, $"Texmods need gMod.dll, which isn't next to the launcher yet: {gmod}", null, true));
        }

        if (needsLoader && !File.Exists(WineLauncher.InjectorPath))
        {
            issues.Add(Blocking("The DLL loader isn't installed, so mods can't be loaded."));
        }

        return issues;
    }

    // Asks ArenaNet for the latest build, so it's kept out of Check, which only looks at local files. Runs when the
    // issues window opens, not on every refresh. Skipped when the exe itself is unusable, since Check already reports that.
    public static async Task<List<AccountIssue>> CheckGwExeVersionAsync(LinuxAccount account)
    {
        var gwPath = account.EffectiveGwPath;
        if (WindowsPathPattern().IsMatch(gwPath) || !FileCheck.IsUsableFile(gwPath))
        {
            return new List<AccountIssue>();
        }

        using var timeout = new CancellationTokenSource(GwVersionCheckTimeout);
        try
        {
            var outdated = await GwExeUpdater.IsOutdatedAsync(gwPath, timeout.Token);
            return outdated
                ? new List<AccountIssue> { new(IssueSeverity.Blocking, "Gw.exe is out of date. Update it to match the latest game build.", null, false, CanUpdateGwExe: true) }
                : new List<AccountIssue>();
        }
        catch (Exception ex)
        {
            var reason = timeout.IsCancellationRequested ? "ArenaNet didn't answer in time" : ex.Message;
            return new List<AccountIssue> { new(IssueSeverity.Warning, $"Couldn't check whether Gw.exe is up to date: {reason}") };
        }
    }

    // Only a confirmed out-of-date exe counts. A failed check lets the launch go ahead, as the Windows launcher does.
    public static async Task<bool> IsGwExeOutdatedAsync(LinuxAccount account) =>
        (await CheckGwExeVersionAsync(account)).Any(issue => issue.CanUpdateGwExe);

    // Adds a blocking issue when the file can't be used, with the reason from ReparseInspector.
    private static void CheckFile(List<AccountIssue> issues, string path, string label)
    {
        if (FileCheck.IsUsableFile(path))
        {
            return;
        }

        var wof = ReparseInspector.IsWofStub(path);
        issues.Add(new AccountIssue(IssueSeverity.Blocking, $"{label} is {ReparseInspector.UnusableReason(path)}", wof ? path : null));
    }

    private static string ModLabel(LinuxMod mod)
    {
        var name = mod.FileName.Split('\\', '/').Last();
        return $"{(mod.Type == ModType.kModTypeDLL ? "DLL mod" : "Texmod")} {name}";
    }

    private static AccountIssue Blocking(string message) => new(IssueSeverity.Blocking, message);

    [GeneratedRegex(@"^[A-Za-z]:\\")]
    private static partial Regex WindowsPathPattern();
}
