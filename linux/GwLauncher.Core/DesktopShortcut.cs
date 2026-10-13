using System.Text.RegularExpressions;

namespace GwLauncher.Linux;

// Writes a .desktop launcher that starts one account with `-launch`, like the Windows desktop shortcut.
public static class DesktopShortcut
{
    private const UnixFileMode EntryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                           UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                           UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    public static string Create(LinuxAccount account, string launcher)
    {
        if (Path.GetFileNameWithoutExtension(launcher) == "dotnet")
        {
            throw new InvalidOperationException("Shortcuts need the published launcher, not 'dotnet run'.");
        }

        var directory = DesktopDirectory();
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{SafeFileName(account.Name)}.desktop");
        var content = $"""
            [Desktop Entry]
            Type=Application
            Name={account.Name}
            Comment=GW Launcher shortcut for {account.Name}
            Exec={QuoteExecArg(launcher)} -launch {QuoteExecArg(account.Name)}
            Icon={AppPaths.IconFile}
            StartupWMClass=GW_Launcher
            Path={Path.GetDirectoryName(launcher)}
            Terminal=false
            Categories=Game;
            """;

        File.WriteAllText(path, content.ReplaceLineEndings("\n") + "\n");
        File.SetUnixFileMode(path, EntryMode);
        MarkTrusted(path);
        return path;
    }

    // Follows the user's XDG Desktop setting, which is localised on some desktops.
    public static string DesktopDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userDirs = Path.Combine(home, ".config", "user-dirs.dirs");
        if (File.Exists(userDirs))
        {
            foreach (var line in File.ReadLines(userDirs))
            {
                var match = Regex.Match(line, @"^XDG_DESKTOP_DIR=""(.*)""");
                if (match.Success)
                {
                    return match.Groups[1].Value.Replace("$HOME", home);
                }
            }
        }

        return Path.Combine(home, "Desktop");
    }

    // Desktop Entry Exec quoting: escape for the quoted argument, then escape backslashes for the string value.
    private static string QuoteExecArg(string arg)
    {
        var escaped = Regex.Replace(arg, @"[\\""`$]", match => "\\" + match.Value);
        return "\"" + escaped.Replace("\\", "\\\\").Replace("%", "%%") + "\"";
    }

    private static string SafeFileName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars())).Trim();

    // GNOME-based desktops won't launch a desktop icon until it's marked trusted. Best effort: gio may not be installed.
    private static void MarkTrusted(string path)
    {
        try
        {
            var psi = new ProcessStartInfo("gio")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("set");
            psi.ArgumentList.Add(path);
            psi.ArgumentList.Add("metadata::trusted");
            psi.ArgumentList.Add("true");

            using var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }
}
