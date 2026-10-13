namespace GwLauncher.Linux;

// Runs setup commands detached from the launcher. Output goes to a log file rather than a pipe,
// so a setup command keeps running, and isn't killed mid-way, if the launcher exits.
public static class SetupRunner
{
    public static Process Start(string command, bool needsRoot)
    {
        Directory.CreateDirectory(AppPaths.DataDir);

        var inner = needsRoot ? $"pkexec sh -c {Quote(command)}" : $"sh -c {Quote(command)}";
        var psi = new ProcessStartInfo
        {
            FileName = "sh",
            ArgumentList = { "-c", $"{inner} > {Quote(LogPath)} 2>&1" },
            UseShellExecute = false
        };

        var process = Process.Start(psi) ?? throw new InvalidOperationException("The setup command could not be started.");
        File.WriteAllText(PidPath, process.Id.ToString());
        return process;
    }

    // True while a command started by an earlier launcher session is still running.
    public static bool InProgress()
    {
        if (!File.Exists(PidPath) || !int.TryParse(File.ReadAllText(PidPath).Trim(), out var pid))
        {
            return false;
        }

        if (Directory.Exists($"/proc/{pid}"))
        {
            return true;
        }

        ClearMarker();
        return false;
    }

    public static void ClearMarker()
    {
        if (File.Exists(PidPath))
        {
            File.Delete(PidPath);
        }
    }

    // Last non-empty line of the log, for showing progress.
    public static string LastLogLine()
    {
        if (!File.Exists(LogPath))
        {
            return "";
        }

        try
        {
            using var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";
        }
        catch (IOException)
        {
            return "";
        }
    }

    public static string LogPath => Path.Combine(AppPaths.DataDir, "setup.log");

    private static string PidPath => Path.Combine(AppPaths.DataDir, "setup.pid");

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";
}
