namespace GW_Launcher.Classes;
public enum ModType
{
    kModTypeTexmod,
    kModTypeDLL
}

public class Mod
{
    public ModType type;
    public string fileName = "";
    public bool active;
}
