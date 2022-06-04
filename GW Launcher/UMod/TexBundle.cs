namespace GW_Launcher.uMod;

internal struct TexDef
{
    public uint crcHash;
    public byte[] fileData;
}
public class TexBundle
{
    public string name;
    internal List<TexDef> defs = new();

    public TexBundle(string filePath)
    {
        name = filePath.Split('\\').Last();
        Load(filePath);
    }

    public void Load(string filePath)
    {
        var loader = new ZipLoader(filePath);
        foreach (var (crc, value) in loader.Entries)
        {
            TexDef def;
            def.crcHash = Convert.ToUInt32(crc, 16);
            def.fileData = value;

            defs.Add(def);
        }
    }

    public void Dispose()
    {
        defs.Clear();
    }
}