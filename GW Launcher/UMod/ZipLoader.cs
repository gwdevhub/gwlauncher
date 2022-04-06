﻿using System.IO.Compression;
using System.Text;
using Ionic.Zip;

namespace GW_Launcher.UMod;

public class ZipLoader
{
    private readonly byte[] _tpfPassword =
    {
        0x73, 0x2A, 0x63, 0x7D, 0x5F, 0x0A, 0xA6, 0xBD, //90, 67,
        0x7D, 0x65, 0x7E, 0x67, 0x61, 0x2A, 0x7F, 0x7F,
        0x74, 0x61, 0x67, 0x5B, 0x60, 0x70, 0x45, 0x74,
        0x5C, 0x22, 0x74, 0x5D, 0x6E, 0x6A, 0x73, 0x41,
        0x77, 0x6E, 0x46, 0x47, 0x77, 0x49, 0x0C, 0x4B,
        0x46, 0x6F, 0
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
            using var z = new Ionic.Zip.ZipFile(fileName, Encoding.Default);
            z.Password = Encoding.Default.GetString(_tpfPassword);
            z.Encryption = EncryptionAlgorithm.PkzipWeak; // the default: you might need to select the proper value here

            var files = new Dictionary<string, byte[]>();
            foreach (var zEntry in z)
            {
                MemoryStream tempS = new MemoryStream();
                zEntry.Extract(tempS);

                files.Add(zEntry.FileName, tempS.ToArray());
            }

            Entries = files;
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