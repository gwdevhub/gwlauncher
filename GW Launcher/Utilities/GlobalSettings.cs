namespace GW_Launcher.Utilities;

public class GlobalSettings
{
    private GlobalSettings()
    {
        Encrypt = true;
        CheckForUpdates = true;
        AutoUpdate = false;
    }

    public bool Encrypt { get; set; }

    public bool CheckForUpdates { get; set; }

    public bool AutoUpdate { get; set; }

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
            var settings = new GlobalSettings();
            var result = MessageBox.Show(@"Would you like to encrypt the account info?", @"Encryption",
                MessageBoxButtons.YesNo);
            if (result == DialogResult.No)
            {
                settings.Encrypt = false;
            }

            return settings;
        }
    }
}
