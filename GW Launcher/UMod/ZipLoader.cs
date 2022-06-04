using Ionic.Zip;

namespace GW_Launcher.uMod;

public class ZipLoader
{
    private readonly byte[] _tpfPassword =
    {
        0x73, 0x2A, 0x63, 0x7D, 0x5F, 0x0A, 0xA6, 0xBD,
        0x7D, 0x65, 0x7E, 0x67, 0x61, 0x2A, 0x7F, 0x7F,
        0x74, 0x61, 0x67, 0x5B, 0x60, 0x70, 0x45, 0x74,
        0x5C, 0x22, 0x74, 0x5D, 0x6E, 0x6A, 0x73, 0x41,
        0x77, 0x6E, 0x46, 0x47, 0x77, 0x49, 0x0C, 0x4B,
        0x46, 0x6F
    };

    public ZipLoader(string fileName)
    {
        Entries = new ReadOnlyDictionary<string, byte[]>(new Dictionary<string, byte[]>());
        var ext = Path.GetExtension(fileName);
        var IsTpfEncrypted = ext == ".tpf";
        var files = new Dictionary<string, byte[]>();
        var contents = new Dictionary<string, byte[]>();

        if (IsTpfEncrypted)
        {
            using var file = new uModFile(fileName);
            var fileContent = file.GetContent();
            if (fileContent == null) return;
            using var memoryStream = new MemoryStream(fileContent);
            using var archive = ZipFile.Read(memoryStream);
            var password = Encoding.Latin1.GetString(_tpfPassword);
            archive.Password = password;
            archive.Encryption = EncryptionAlgorithm.None;

            foreach (var entry in archive.Entries)
            {
                var content = new byte[entry.UncompressedSize];
                entry.Extract(new MemoryStream(content));
                files[entry.FileName] = content;
            }
        }
        else
        {
            using var stream = new FileStream(fileName, FileMode.Open);
            var archive = new ZipFile(fileName);
            foreach (var entry in archive.Entries)
            {
                var content = new byte[entry.UncompressedSize];
                entry.Extract(new MemoryStream(content));
                files[entry.FileName] = content;
            }
        }

        if (files.ContainsKey("texmod.def"))
        {
            var texcontent = files["texmod.def"];
            var text = Encoding.Default.GetString(texcontent);
            var lines = text.Replace("\r", "").Split('\n');
            foreach (var line in lines)
            {
                var splits = line.Split('|');
                if (splits.Length != 2) continue;

                var addrstr = splits[0];
                var path = splits[1];
                while (path[0] == '.' && (path[1] == '/' || path[1] == '\\') || path[0] == '/' || path[0] == '\\') path = path.Remove(0, 1);

                if (!files.ContainsKey(path)) continue;
                files.Remove(path, out var content);
                Debug.Assert(content != null, nameof(content) + " != null");
                contents[addrstr] = content;
            }

            Entries = contents;
        }
        else
        {
            foreach (var filename in files.Keys)
            {
                // GW.EXE_0x12345678.dds
                files.Remove(filename, out var content);
                if (content == null) continue;

                var name = filename;
                while (name.Contains('_'))
                {
                    var firstIndex = name.LastIndexOf('_');
                    name = ++firstIndex >= filename.Length - 1 ? filename : filename[firstIndex..];
                }

                if (name.Contains('.'))
                {
                    var lastIndex = name.LastIndexOf('.');
                    name = name[..lastIndex];
                }
                // 0x18F22DA3
                var crc = name;

                contents[crc] = content;
            }
            Entries = contents;
        }
        GC.Collect();
    }

    public IReadOnlyDictionary<string, byte[]> Entries { get; }
}