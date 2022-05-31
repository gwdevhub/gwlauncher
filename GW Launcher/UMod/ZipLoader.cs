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
        var ext = Path.GetExtension(fileName);
        if (ext == ".tpf")
        {
            IsTpfEncrypted = true;
        }

        if (IsTpfEncrypted)
        {
            var file = new uModFile(fileName);
            if (file.GetFile() == null) return;
            using var memoryStream = new MemoryStream(file.GetFile());
            using var archive = Ionic.Zip.ZipFile.Read(memoryStream);
            var password = Encoding.Latin1.GetString(_tpfPassword);
            archive.Password = password;
            archive.Encryption = EncryptionAlgorithm.None;
            archive.StatusMessageTextWriter = Console.Out;

            var contents = new Dictionary<string, byte[]>();
            foreach (var entry in archive.Entries)
            {
                var content = new byte[entry.UncompressedSize];
                entry.ExtractWithPassword(new MemoryStream(content), password);

                var curFileName = entry.FileName;

                if (entry.FileName.Contains("copy"))
                {
                    curFileName = entry.FileName.Substring(0, 17) + Path.GetExtension(entry.FileName);
                }
                contents[curFileName] = content;
            }

            Entries = contents;
        }
        else
        {
            using var stream = new FileStream(fileName, FileMode.Open);
            var archive = new ZipArchive(stream);
            var files = new Dictionary<string, byte[]>();
            foreach (var entry in archive.Entries)
            {
                MemoryStream tempS = new MemoryStream();
                using var file = entry.Open();
                {
                    var fileData = new byte[entry.Length];
                    file.Read(fileData, 0, (int)entry.Length);
                    files.Add(entry.Name, fileData);
                }
            }

            Entries = files;
        }
    }

    public IReadOnlyDictionary<string, byte[]> Entries { get; }

    public bool IsTpfEncrypted { get; set; }
    
}