using System.Text.RegularExpressions;

namespace GwLauncher.Linux;

public static partial class WineLauncher
{
    // Dedicated prefix so the launcher never shares state with other Wine apps.
    public static string PrefixPath =>
        Path.Combine(
            Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share"),
            "gwlauncher",
            "prefix");

    public static string WineBinary =>
        Environment.GetEnvironmentVariable("GWLAUNCHER_WINE") is { Length: > 0 } wine ? wine : "wine";

    // The Windows helper that starts the game suspended and loads DLLs into it.
    public static string InjectorPath =>
        Environment.GetEnvironmentVariable("GWLAUNCHER_INJECTOR") is { Length: > 0 } injector
            ? injector
            : Path.Combine(AppContext.BaseDirectory, "injector", "GwLauncher.Injector.exe");

    public static ProcessStartInfo BuildStartInfo(LinuxAccount account)
    {
        var blocking = AccountDiagnostics.Check(account).Where(issue => issue.Severity == IssueSeverity.Blocking).ToList();
        if (blocking.Count > 0)
        {
            throw new AccountBlockedException(account.Name, blocking);
        }

        var gwPath = account.EffectiveGwPath;

        if (WindowsPathPattern().IsMatch(gwPath))
        {
            throw new InvalidOperationException(
                $"'{gwPath}' is a Windows path with no Linux path set. Set the account's Linux path to Gw.exe.");
        }

        if (!FileCheck.IsUsableFile(gwPath))
        {
            throw new FileNotFoundException($"Gw.exe not found for '{account.Name}'.", gwPath);
        }

        var gameArguments = GameArguments(account, gwPath);
        var dlls = LaunchDlls(account);

        var psi = new ProcessStartInfo
        {
            FileName = WineBinary,
            WorkingDirectory = Path.GetDirectoryName(gwPath)!,
            UseShellExecute = false
        };
        psi.Environment["WINEPREFIX"] = PrefixPath;

        if (dlls.Count == 0)
        {
            foreach (var argument in gameArguments)
            {
                psi.ArgumentList.Add(argument);
            }

            return psi;
        }

        if (!File.Exists(InjectorPath))
        {
            throw new FileNotFoundException(
                "The DLL loader isn't installed, so mods can't be loaded. Build the GwLauncher.Injector project and copy its publish folder to an injector folder next to the launcher.",
                InjectorPath);
        }

        psi.ArgumentList.Add(WindowsPaths.ToWine(InjectorPath));
        foreach (var dll in dlls)
        {
            psi.ArgumentList.Add("--dll");
            psi.ArgumentList.Add(WindowsPaths.ToWine(dll));
        }

        psi.ArgumentList.Add("--timeout");
        psi.ArgumentList.Add(LoadTimeoutMs().ToString());
        psi.ArgumentList.Add("--");
        psi.ArgumentList.Add(WindowsPaths.ToWine(gameArguments[0]));
        foreach (var argument in gameArguments.Skip(1))
        {
            psi.ArgumentList.Add(argument);
        }

        return psi;
    }

    // The game's own command line, starting with Gw.exe's Linux path.
    private static List<string> GameArguments(LinuxAccount account, string gwPath)
    {
        var arguments = new List<string> { gwPath, "-email", account.Email, "-password", account.Password };

        if (!string.IsNullOrEmpty(account.Character))
        {
            arguments.Add("-character");
            arguments.Add(account.Character);
        }

        // Whitespace split only; quoted extra args with spaces aren't supported yet.
        arguments.AddRange(account.ExtraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return arguments;
    }

    // Active DLL mods from the account, then plugin-folder DLLs when the account uses them. Same order as the Windows launcher.
    public static List<string> DllsToLoad(LinuxAccount account)
    {
        var dlls = account.Mods
            .Where(mod => mod.Active && mod.Type == ModType.Dll && mod.IsAvailable)
            .Select(mod => mod.EffectivePath)
            .ToList();

        if (account.UsePluginFolderMods)
        {
            dlls.AddRange(LinuxMod.PluginFolderFiles(account.EffectiveGwPath)
                .Where(plugin => plugin.Type == ModType.Dll && plugin.TargetPath is { } target && FileCheck.IsUsableFile(target))
                .Select(plugin => plugin.TargetPath!));
        }

        return dlls;
    }

    // What the game is injected with. Texmods only load through gMod, so it's added when the account has texmods and
    // no active DLL is gMod already. AccountDiagnostics blocks the launch if that copy isn't usable.
    public static List<string> LaunchDlls(LinuxAccount account)
    {
        var dlls = DllsToLoad(account);
        if (TexmodsToLoad(account).Count > 0 && !dlls.Any(IsGMod))
        {
            dlls.Add(GModProvider.LocalPath);
        }

        return dlls;
    }

    public static bool IsGMod(string dll) => Path.GetFileName(dll).Equals("gMod.dll", StringComparison.OrdinalIgnoreCase);

    // Active texmods from the account, then plugin-folder texmods when the account uses them.
    public static List<string> TexmodsToLoad(LinuxAccount account)
    {
        var texmods = account.Mods
            .Where(mod => mod.Active && mod.Type == ModType.Texmod && mod.IsAvailable)
            .Select(mod => mod.EffectivePath)
            .ToList();

        if (account.UsePluginFolderMods)
        {
            texmods.AddRange(LinuxMod.PluginFolderFiles(account.EffectiveGwPath)
                .Where(plugin => plugin.Type == ModType.Texmod && plugin.TargetPath is { } target && FileCheck.IsUsableFile(target))
                .Select(plugin => plugin.TargetPath!));
        }

        return texmods;
    }

    // Everything the game needs written before it starts. Dry runs skip this.
    public static void PrepareLaunch(LinuxAccount account)
    {
        WriteGameRegistry(account);
        WriteModList(account);
    }

    // gMod reads the active texmods from modlist.txt next to gMod.dll. Paths are Windows paths because gMod runs inside Wine.
    public static void WriteModList(LinuxAccount account)
    {
        var texmods = TexmodsToLoad(account).Select(WindowsPaths.ToWine).ToList();
        if (texmods.Count == 0)
        {
            return;
        }

        var gmod = LaunchDlls(account).FirstOrDefault(IsGMod);
        if (gmod == null)
        {
            throw new GModUnavailableException("Texmods are active, but gMod.dll couldn't be found or downloaded.");
        }

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(gmod)!, "modlist.txt"), string.Join('\n', texmods));
    }

    // The game reads its install path from the registry, which the Windows launcher writes before every start.
    public static void WriteGameRegistry(LinuxAccount account)
    {
        Directory.CreateDirectory(PrefixPath);

        // Wine maps the Linux root to Z:, so the Linux path becomes the matching Windows path.
        var windowsPath = WindowsPaths.ToWine(account.EffectiveGwPath);

        foreach (var valueName in new[] { "Path", "Src" })
        {
            var psi = new ProcessStartInfo
            {
                FileName = WineBinary,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.Environment["WINEPREFIX"] = PrefixPath;
            psi.ArgumentList.Add("reg");
            psi.ArgumentList.Add("add");
            psi.ArgumentList.Add(@"HKCU\Software\ArenaNet\Guild Wars");
            psi.ArgumentList.Add("/v");
            psi.ArgumentList.Add(valueName);
            psi.ArgumentList.Add("/t");
            psi.ArgumentList.Add("REG_SZ");
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add(windowsPath);
            psi.ArgumentList.Add("/f");

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("wine could not be started.");
            process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Could not set the game's {valueName} registry value: {error.Trim()}");
            }
        }
    }

    // Starts the game. Through the injector, the loader exits once the game is resumed, so a non-zero exit means the start failed.
    public static int Start(ProcessStartInfo psi)
    {
        var viaInjector = psi.ArgumentList.Any(arg => arg.EndsWith("GwLauncher.Injector.exe", StringComparison.OrdinalIgnoreCase));

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("wine could not be started.");
        if (!viaInjector || !process.WaitForExit(20000))
        {
            return process.Id;
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The game didn't start cleanly (loader exit code {process.ExitCode}). Check the account's mods and game path.");
        }

        return process.Id;
    }

    // Shows the command for logs and dry runs, with the password masked.
    public static string Describe(ProcessStartInfo psi)
    {
        var parts = new List<string> { psi.FileName };
        var maskNext = false;

        foreach (var arg in psi.ArgumentList)
        {
            parts.Add(maskNext ? "********" : arg);
            maskNext = arg == "-password";
        }

        return $"WINEPREFIX={psi.Environment["WINEPREFIX"]} {string.Join(' ', parts)}";
    }

    // The mod-launch timeout from Settings.json, falling back to the default if the file can't be read.
    private static uint LoadTimeoutMs()
    {
        try
        {
            return AppSettings.Load().TimeoutOnModlaunch;
        }
        catch (Exception ex) when (ex is IOException or Newtonsoft.Json.JsonException)
        {
            return 5000;
        }
    }

    [GeneratedRegex(@"^[A-Za-z]:\\")]
    private static partial Regex WindowsPathPattern();
}
