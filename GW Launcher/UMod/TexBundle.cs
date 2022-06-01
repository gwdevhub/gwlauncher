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
            // GW.EXE_0x18F22DA3.dds
            var tmp = key.Split('_', '.');

            if (tmp.Length != 4)
            {
                continue;
            }

            // string exeName = tmp[0] + "." + tmp[1];
            var crc = tmp[2];
            // string textureType = tmp[3];

            TexDef def;
            def.fileName = key;
            def.crcHash = Convert.ToUInt32(crc, 16);
            def.fileData = value;

            defs.Add(def);
        }
    }
}