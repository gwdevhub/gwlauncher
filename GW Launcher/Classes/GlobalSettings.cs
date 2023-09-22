namespace GW_Launcher.Classes;

public class GlobalSettings
{
    public bool Encrypt { get; set; } = true;

    public bool CheckForUpdates { get; set; } = true;

    public bool AutoUpdate { get; set; } = false;

    public bool LaunchMinimized { get; set; } = false;

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
