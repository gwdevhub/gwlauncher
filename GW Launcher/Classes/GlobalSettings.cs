namespace GW_Launcher.Classes;

public class GlobalSettings
{
    public bool CheckForUpdates { get; set; } = true;

    public bool AutoUpdate { get; set; } = false;

    public bool LaunchMinimized { get; set; } = false;

    public uint TimeoutOnModlaunch { get; set; } = 5000;

    public void Save(string path = "Settings.json")
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public static GlobalSettings Load(string path = "Settings.json")
    {
        try
        {
            var text = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<GlobalSettings>(text) ?? new GlobalSettings();
        }
        catch (FileNotFoundException)
        {
            // Encryption is now opt-in via the master password field in Settings, and is
            // auto-detected from Accounts.json, so there is nothing to ask about on first run.
            return new GlobalSettings();
        }
    }
}
