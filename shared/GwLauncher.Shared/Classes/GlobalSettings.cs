namespace GW_Launcher.Classes;

public class GlobalSettings
{
    public bool CheckForUpdates { get; set; } = true;

    public bool AutoUpdate { get; set; } = false;

    public bool LaunchMinimized { get; set; } = true;

    public bool KeepInSystemTray { get; set; } = true;

    // The account list's size, kept as the user last left it.
    public int ListWidth { get; set; } = 220;

    public int ListHeight { get; set; } = 340;

    // Where the account list was last left outside tray mode. Null until it's been moved.
    public int? ListX { get; set; }

    public int? ListY { get; set; }

    // Read-only migration of the old inverse setting, so existing Settings.json files keep their behaviour.
    [JsonProperty("KeepLauncherOpen")]
    private bool? LegacyKeepLauncherOpen
    {
        set
        {
            if (value.HasValue)
                KeepInSystemTray = !value.Value;
        }
    }

    public uint TimeoutOnModlaunch { get; set; } = 10000;

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
