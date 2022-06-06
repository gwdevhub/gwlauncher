namespace GW_Launcher.Classes;

public enum ModType
{
    kModTypeTexmod,
    kModTypeDLL
}

public class Mod
{
    public bool active;
    public string fileName = "";
    public ModType type;
}
