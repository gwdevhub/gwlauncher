using Microsoft.Web.WebView2.Core;

namespace GW_Launcher.Platform;

// The window is drawn by WebView2. When its runtime is missing, this offers to download and run Microsoft's installer.
internal static class RendererCheck
{
    private const string BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private const string Manual = "Install it from https://developer.microsoft.com/microsoft-edge/webview2/, then start GW Launcher again.";

    public static bool Ensure()
    {
        if (IsInstalled())
        {
            return true;
        }

        var answer = MessageBox.Show(
            "GW Launcher needs the Microsoft Edge WebView2 Runtime to show its window, and it isn't installed.\n\nDownload and install it now?",
            "GW Launcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
        {
            return false;
        }

        try
        {
            var installer = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
            using (var http = new HttpClient())
            {
                File.WriteAllBytes(installer, http.GetByteArrayAsync(BootstrapperUrl).GetAwaiter().GetResult());
            }

            // Shell execution lets the installer ask for administrator rights when it needs them.
            using var process = Process.Start(new ProcessStartInfo(installer, "/silent /install") { UseShellExecute = true });
            process?.WaitForExit();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or Win32Exception)
        {
            MessageBox.Show($"The WebView2 Runtime couldn't be installed: {ex.Message}\n\n{Manual}", "GW Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        if (IsInstalled())
        {
            return true;
        }

        MessageBox.Show($"The WebView2 Runtime still isn't available.\n\n{Manual}", "GW Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }

    private static bool IsInstalled()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }
    }
}
