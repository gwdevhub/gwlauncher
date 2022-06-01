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

        var loader = new ZipLoader(filePath);
        foreach (var (key, value) in loader.Entries)
        {
            // 0x18F22DA3
            var crc = key;
            switch (crc.Length)
            {
                case < 10:
                    continue;
                case > 10:
                    crc = crc[..10];
                    break;
            }

            TexDef def;
            def.crcHash = Convert.ToUInt32(crc, 16);
            def.fileData = value;

            defs.Add(def);
        }
    }
}