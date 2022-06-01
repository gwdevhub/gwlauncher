﻿using System.Collections.ObjectModel;

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
        if (ext == ".tpf")
        {
            IsTpfEncrypted = true;
        }
        var files = new Dictionary<string, byte[]>();
        var contents = new Dictionary<string, byte[]>();

        if (IsTpfEncrypted)
        {
            using var file = new uModFile(fileName);
            var fileContent = file.GetContent();
            if (fileContent == null) return;
            using var memoryStream = new MemoryStream(fileContent);
            using var archive = Ionic.Zip.ZipFile.Read(memoryStream);
            var password = Encoding.Latin1.GetString(_tpfPassword);
            archive.Password = password;
            archive.Encryption = EncryptionAlgorithm.None;
            
            foreach (var entry in archive.Entries)
            {
                var content = new byte[entry.UncompressedSize];
                entry.Extract(new MemoryStream(content));

                var curFileName = entry.FileName;
                files[curFileName] = content;
            }
        }
        else
        {
            using var stream = new FileStream(fileName, FileMode.Open);
            var archive = new ZipArchive(stream);
            foreach (var entry in archive.Entries)
            {
                MemoryStream tempS = new MemoryStream();
                using var file = entry.Open();
                {
                    var fileData = new byte[entry.Length];
                    var readBytes = file.Read(fileData, 0, (int)entry.Length);
                    if (readBytes == entry.Length)
                    {
                        files.Add(entry.Name, fileData);
                    }
                }
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
                // GW.EXE_0xE386EED8.dds
                files.Remove(filename, out var content);
                if (content == null) continue;
                
                var splits = filename.Split('.', '_');
                if (splits.Length != 4) continue;
                if (splits[0] != "GW" || splits[1] != "exe") continue;
                var address = splits[2];
                if (address.Length == 10)
                {
                    contents[address] = content;
                }
            }
            Entries = contents;
        }
        GC.Collect();
    }

    public IReadOnlyDictionary<string, byte[]> Entries { get; }

    public bool IsTpfEncrypted { get; set; }
    
}