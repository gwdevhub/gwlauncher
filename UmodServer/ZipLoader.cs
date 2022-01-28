using System;
using System.Collections.Generic;

using System.IO;
using System.IO.Compression;

namespace UmodServer
{
    public class ZipLoader : IDisposable
    {
        FileStream stream;
        ZipArchive archive;

        readonly byte[] tpfPassword =
        {
            0x73, 0x2A, 0x63, 0x7D, 0x5F, 0x0A, 0xA6, 0xBD,
            0x7D, 0x65, 0x7E, 0x67, 0x61, 0x2A, 0x7F, 0x7F,
            0x74, 0x61, 0x67, 0x5B, 0x60, 0x70, 0x45, 0x74,
            0x5C, 0x22, 0x74, 0x5D, 0x6E, 0x6A, 0x73, 0x41,
            0x77, 0x6E, 0x46, 0x47, 0x77, 0x49, 0x0C, 0x4B,
            0x46, 0x6F, 0
        };

        public ZipLoader(string fileName)
        {
            string ext = fileName.Substring(fileName.Length - 4);
            switch (ext)
            {
                case ".tpf":
                    IsTpfEncrypted = true;
                    throw new NotImplementedException("Dont use stupidly encrypted zip files thx.");
            }

            stream = new FileStream(fileName, FileMode.Open);
            archive = new ZipArchive(stream);
        }


        public IReadOnlyCollection<ZipArchiveEntry> Entries => archive.Entries;

        public bool IsTpfEncrypted { get; set; }

        public void Dispose()
        {
            archive.Dispose();
            stream.Dispose();
        }
    }
}
