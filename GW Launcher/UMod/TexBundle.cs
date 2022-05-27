namespace GW_Launcher.uMod;

internal struct TexDef
{
    public uint crcHash;
    public string fileName;
    public byte[] fileData;
}
public class TexBundle
{
    public string name;
    internal List<TexDef> defs = new();

    public TexBundle(string filePath)
    {
        name = filePath.Split('\\').Last();

        var loader = new ZipLoader(filePath);
        foreach (var (key, value) in loader.Entries)
        {
            var tmp = key.Split('_', '.');

            if (tmp.Length != 4)
            {
                continue;
                throw new Exception("Not using a texmod created texture :s");
            }

            // string exeName = tmp[0];
            var crc = tmp[2];
            // string textureType = tmp[2];

            TexDef def;
            def.fileName = key;
            def.crcHash = Convert.ToUInt32(crc, 16);
            def.fileData = value;

            defs.Add(def);
        }
    }
}