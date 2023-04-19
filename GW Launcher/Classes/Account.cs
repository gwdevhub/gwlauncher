namespace GW_Launcher.Classes;

public class Account
{
    [JsonIgnore] public string state = "Inactive";

    [JsonRequired] public string character = "";

    public bool elevated;

    [JsonRequired] public string email = "";

    public string extraargs = "";

    [JsonRequired] public string gwpath = "";

    public List<Mod> mods = new();

    [JsonRequired] public string password = "";

    [JsonIgnore] public GWCAMemory? process;

    public string title = "";

    public Guid guid = Guid.NewGuid();

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
