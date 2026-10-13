using System.Security.Principal;

namespace GW_Launcher.Utilities;

internal static class AdminAccess
{
    public static bool HasAdmin()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }

    // Restarts the launcher elevated, passing arguments to the new copy. Returns true when already elevated, and false when
    // the restart was started or the user cancelled the prompt. Callers stop on false.
    public static bool RestartAsAdminPrompt(string arguments = "restart")
    {
        if (HasAdmin())
        {
            return true;
        }

        var processInfo = new ProcessStartInfo
        {
            Verb = "runas",
            UseShellExecute = true,
            FileName = Environment.ProcessPath,
            Arguments = arguments
        };

        try
        {
            Program.GwlMutex?.Close();
            Process.Start(processInfo);
            Environment.Exit(0);
            return false;
        }
        catch (Win32Exception)
        {
            // The user cancelled the prompt.
            return false;
        }
    }
}
