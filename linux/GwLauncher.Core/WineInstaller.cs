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
    public static string? InstallCommand() => Distro.InstallCommand(
        "apt-get update && (apt-get install -y wine64 || apt-get install -y wine)",
        "dnf install -y wine",
        "pacman -S --noconfirm wine",
        "zypper --non-interactive install wine");
}
