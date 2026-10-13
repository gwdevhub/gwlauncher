using Avalonia.Controls;
using GwLauncher.Linux;

namespace GwLauncher.Tray;

// Runs the Gw.exe update behind a progress dialog. Returns a status message, or null when it was cancelled or failed.
public static class GwExeFix
{
    public static async Task<string?> RunAsync(Window? owner, LinuxAccount account)
    {
        var gwPath = account.EffectiveGwPath;
        var updated = await ProgressDialog.RunAsync<string>(
            owner,
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
}
