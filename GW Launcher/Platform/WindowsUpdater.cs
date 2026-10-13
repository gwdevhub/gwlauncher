using System.Text.RegularExpressions;
using GwLauncher.Ui;
using GW_Launcher.Guildwars;
using GW_Launcher.Guildwars.Utils;
using GW_Launcher.Launcher;

namespace GW_Launcher.Platform;

// The update checks the Windows launcher runs in the background: the launcher itself, gMod.dll, and Gw.exe.
// Questions and progress go to the page through the prompts the bridge provides.
internal static class WindowsUpdater
{
    public static async Task CheckLauncherUpdate(IPrompts prompts)
    {
        var currentPath = Environment.ProcessPath;
        var exeDir = currentPath == null ? null : Path.GetDirectoryName(currentPath);
        if (currentPath == null || exeDir == null)
        {
            return;
        }

        // Staging files sit beside the exe, not in the working directory, which is e.g. System32 when launched from Startup.
        var oldPath = Path.Combine(exeDir, ".old.exe");
        var newPath = Path.Combine(exeDir, ".new.exe");
        TryDelete(oldPath);
        TryDelete(newPath);

        var releases = await GitHubAssets.GetReleasesAsync("gwdevhub", "gwlauncher");
        var release = releases.FirstOrDefault(r => !r.Prerelease && !r.Draft);
        if (release == null)
        {
            return;
        }

        var tagName = Regex.Replace(release.TagName, @"[^\d\.]", "");
        if (new Version(tagName).CompareTo(new Version("13.0")) <= 0)
        {
            return;
        }

        var assetName = IsDotNet10DesktopInstalled() ? "GW_Launcher_Framework_Dependent.exe" : "GW_Launcher.exe";
        var asset = release.Assets.FirstOrDefault(a => a.Name == assetName);
        if (asset == null || GitHubAssets.IsUpToDate(currentPath, asset.Sha256))
        {
            return;
        }

        if (!Program.Settings.AutoUpdate)
        {
            var wanted = await prompts.Confirm("GW Launcher update",
                $"A different build of GW Launcher is available.\nLatest: {tagName} ({GitHubAssets.ShortSha(asset.Sha256)})\n\nDownload and install it now?");
            if (!wanted)
            {
                return;
            }
        }

        var downloaded = await prompts.Progress<string>(
            new ProgressOptions($"Updating GW Launcher to {tagName}", "Downloading update...", FailureText: "The update couldn't be downloaded"),
            async (cancellation, progress) =>
            {
                await DownloadAsync(new Uri(asset.DownloadUrl), newPath, progress, cancellation);
                return newPath;
            });
        if (downloaded == null)
        {
            TryDelete(newPath);
            return;
        }

        Program.GwlMutex?.Close();

        // Swap the new exe in. On failure roll back, so the exe at currentPath always works.
        try
        {
            File.Move(currentPath, oldPath);
            try
            {
                File.Move(newPath, currentPath);
            }
            catch
            {
                File.Move(oldPath, currentPath);
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update failed to install the new exe; keeping the current version: {ex.Message}");
        }

        Relaunch(currentPath, exeDir);
        Environment.Exit(0);
    }

    public static async Task RefreshGMod()
    {
        var location = Path.GetDirectoryName(AppContext.BaseDirectory);
        var gmod = Path.Combine(location!, "gMod.dll");
        var releases = await GitHubAssets.GetReleasesAsync("gwdevhub", "gMod");
        var asset = releases
            .Where(release => !release.Prerelease && !release.Draft)
            .Select(release => release.Assets.FirstOrDefault(candidate => candidate.Name == "gMod.dll"))
            .FirstOrDefault(candidate => candidate != null);
        if (asset == null || GitHubAssets.IsUpToDate(gmod, asset.Sha256))
        {
            return;
        }

        using var http = new HttpClient();
        await File.WriteAllBytesAsync(gmod, await http.GetByteArrayAsync(new Uri(asset.DownloadUrl)));
    }

    // Asks ArenaNet for the newest Gw.exe, then offers to update every account whose copy is older.
    public static async Task CheckGwExeUpdates(IPrompts prompts, IReadOnlyList<Account> accounts)
    {
        var (latest, _) = await GuildwarsLatestExe.GetAsync();
        if (latest == null)
        {
            return;
        }

        var outdated = accounts.Where(account => IsOutdated(account, latest.Value.FileId)).ToList();
        if (outdated.Count == 0)
        {
            return;
        }

        var names = string.Join(", ", outdated.Select(account => account.Name));
        var wanted = await prompts.Confirm("GW Update", $"These accounts are out of date:\n{names}\n\nUpdate now?");
        if (!wanted)
        {
            return;
        }

        await prompts.Progress<string>(
            new ProgressOptions("Updating Gw.exe", "Downloading the latest Gw.exe from ArenaNet", FailureText: "Gw.exe couldn't be updated"),
            async (cancellation, progress) =>
            {
                await GwDownloader.UpdateClients(outdated,
                    new Progress<(string Stage, double Progress)>(update => progress.Report(new ProgressUpdate(update.Progress, update.Stage))),
                    cancellation);
                return "done";
            });
    }

    // True when the account's Gw.exe build isn't the latest. An exe too old to carry a build ID counts as outdated.
    public static bool IsOutdated(Account account, int latestFileId) =>
        File.Exists(account.gwpath) && new GuildWarsExecutableParser(account.gwpath).GetFileId() != latestFileId;

    private static async Task DownloadAsync(Uri uri, string destination, IProgress<ProgressUpdate> progress, CancellationToken cancellation)
    {
        using var http = new HttpClient();
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
            received += read;
            progress.Report(new ProgressUpdate(
                total is > 0 ? (double)received / total.Value : null,
                $"{received / 1048576.0:0.0} of {(total ?? 0) / 1048576.0:0.0} MB"));
        }
    }

    // Starts the new copy directly, which returns only once the child is running. Falls back to a shell launch.
    private static void Relaunch(string exePath, string workingDir)
    {
        ProcessStartInfo Info(bool useShellExecute) => new()
        {
            FileName = exePath,
            Arguments = "restart",
            WorkingDirectory = workingDir,
            UseShellExecute = useShellExecute
        };

        try
        {
            if (Process.Start(Info(false)) != null)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update direct relaunch failed, trying shell launch: {ex.Message}");
        }

        try
        {
            Process.Start(Info(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update relaunch failed: {ex.Message}");
        }
    }

    // Checks the .NET 10 desktop runtime, which picks the framework-dependent build.
    private static bool IsDotNet10DesktopInstalled()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-runtimes",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            var output = process!.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Contains("Microsoft.WindowsDesktop.App 10");
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Still in use or already gone; the next start tries again.
        }
    }
}
