namespace GwLauncher.Ui;

// Updates each Gw.exe in turn behind one progress view. Each host supplies the update for one path.
public static class ClientUpdates
{
    public static async Task<string> UpdateAll(IPrompts prompts, IReadOnlyList<string> paths,
        Func<string, IProgress<ProgressUpdate>, CancellationToken, Task> update)
    {
        var done = await prompts.Progress<string>(
            new ProgressOptions(
                "Updating Gw.exe",
                "Downloading the latest Gw.exe from ArenaNet",
                "Each distinct Gw.exe is replaced. Gw.dat is left as it is.",
                FailureText: "Gw.exe couldn't be updated"),
            async (cancellation, progress) =>
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    var index = i;
                    var step = new Progress<ProgressUpdate>(update => progress.Report(
                        new ProgressUpdate((index + (update.Fraction ?? 0)) / paths.Count, update.Detail)));
                    await update(paths[i], step, cancellation);
                }

                return "";
            });

        return done == null ? "Gw.exe update was cancelled or failed." : $"Updated Gw.exe for {paths.Count} install(s).";
    }
}
