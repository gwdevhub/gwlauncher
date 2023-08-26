namespace GW_Launcher.Classes;

public class Account
{
    [JsonRequired] public string character = "";
    [JsonRequired] public string email = "";
    [JsonRequired] public string gwpath = "";
    [JsonRequired] public string password = "";
    public string extraargs = "";
    public bool elevated = false;
    public string title = "";
    public bool usePluginFolderMods = true;
    public List<Mod> mods = new();

    [JsonIgnore] public bool active;
    [JsonIgnore] public GWCAMemory? process;
    [JsonIgnore] public Guid? guid = Guid.NewGuid();


    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }

            if (!string.IsNullOrEmpty(character))
            {
                return character;
            }

            return !string.IsNullOrEmpty(email) ? email : character;
        }
    }
}
