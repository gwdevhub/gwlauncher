using GW_Launcher.Classes;

namespace GwLauncher.Linux;

// The Windows launcher's settings model, stored in the Linux data folder rather than beside the Windows exe.
public static class AppSettings
{
    public static string SettingsFile => Path.Combine(AppPaths.DataDir, "Settings.json");

    public static GlobalSettings Load()
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        return GlobalSettings.Load(SettingsFile);
    }

    public static void Save(GlobalSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        settings.Save(SettingsFile);
    }
}
