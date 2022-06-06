namespace GW_Launcher.Utilities;

public class Account
{
    public string title = "";

    [JsonRequired]
    public string email = "";

    [JsonRequired]
    public string password = "";

    [JsonRequired]
    public string character = "";

    [JsonRequired]
    public string gwpath = "";

    public bool elevated;
    public string extraargs = "";
    public List<Mod> mods = new();

    [JsonIgnore]
    public bool active;

    [JsonIgnore]
    public GWCAMemory? process;

    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(title)) return title;
            if (!string.IsNullOrEmpty(character)) return character;
            return !string.IsNullOrEmpty(email) ? email : character;
        }
    }
}
