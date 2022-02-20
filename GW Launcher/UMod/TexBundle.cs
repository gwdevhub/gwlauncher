namespace UMod;

public struct TexDef
{
    public uint crcHash;
    public string fileName;
    public byte[] fileData;
}
public class TexBundle
{
    public string name;
    public List<TexDef> defs = new();

    public TexBundle(string filePath)
    {
        name = filePath.Split('\\').Last();

        var loader = new ZipLoader(filePath);
        foreach (var entry in loader.Entries)
        {
            var tmp = entry.Key.Split('_', '.');

            if (tmp.Length != 3)
                throw new Exception("Not using a texmod created texture :s");

            // string exeName = tmp[0];
            var crc = tmp[1];
            // string textureType = tmp[2];

            TexDef def;
            def.fileName = entry.Key;
            def.crcHash = uint.Parse(crc);
            def.fileData = entry.Value;

            defs.Add(def);
        }
    }
}