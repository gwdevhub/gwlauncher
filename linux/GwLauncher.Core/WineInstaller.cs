namespace GwLauncher.Linux;

public static class WineInstaller
{
    public static bool IsWineAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = WineLauncher.WineBinary,
                ArgumentList = { "--version" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    // Cheap presence check (no process is started), for polling state without spawning wine.
    public static bool IsWineOnPath()
    {
        if (Path.IsPathRooted(WineLauncher.WineBinary))
        {
            return File.Exists(WineLauncher.WineBinary);
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(dir, WineLauncher.WineBinary)));
    }

    // Shell command for the distro's package manager, or null when the distro isn't recognised.
    public static string? InstallCommand()
    {
        var ids = ReadOsReleaseIds();

        if (ids.Contains("debian") || ids.Contains("ubuntu"))
        {
            return "apt-get update && (apt-get install -y wine64 || apt-get install -y wine)";
        }

        if (ids.Contains("fedora") || ids.Contains("rhel"))
        {
            return "dnf install -y wine";
        }

        if (ids.Contains("arch"))
        {
            return "pacman -S --noconfirm wine";
        }

        if (ids.Contains("suse") || ids.Contains("opensuse") || ids.Contains("opensuse-tumbleweed"))
        {
            return "zypper --non-interactive install wine";
        }

        return null;
    }

    private static HashSet<string> ReadOsReleaseIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists("/etc/os-release"))
        {
            return ids;
        }

        foreach (var line in File.ReadAllLines("/etc/os-release"))
        {
            if (line.StartsWith("ID=") || line.StartsWith("ID_LIKE="))
            {
                var value = line[(line.IndexOf('=') + 1)..].Trim('"');
                foreach (var id in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }
}
