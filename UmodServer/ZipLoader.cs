using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Ionic.Zip;

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
            if (ext == ".tpf")
            {
                IsTpfEncrypted = true;
            }

            if (IsTpfEncrypted)
            {
                using (ZipFile archive = new ZipFile(fileName, Encoding.Default))
                {
                    archive.Password = Encoding.Default.GetString(tpfPassword);
                    archive.Encryption = EncryptionAlgorithm.PkzipWeak; // the default: you might need to select the proper value here
                    archive.StatusMessageTextWriter = Console.Out;

                    archive.ExtractAll(@"c:\path\to\unzip\directory\", ExtractExistingFileAction.Throw);
                }
            }
            else
            {
                stream = new FileStream(fileName, FileMode.Open);
                archive = new ZipArchive(stream);
            }
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
