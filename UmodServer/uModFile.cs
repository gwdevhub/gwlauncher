using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Ionic.Zip;

namespace UmodServer
{
    public class uModFile
    {
        public uModFile(string fileName = null)
        {
            FileName = fileName;
        }

        public bool FileSupported()
        {
            return FileName.EndsWith(".tpf") || FileName.EndsWith(".zip");
        }

        public int GetComment(string tool_tip)
        {
            return 0;
        }

        public AddTextureClass GetContent(bool add)
        {
            string file_type = Path.GetExtension(FileName);
            if (file_type != ".zip" && file_type != ".tpf") return null;
            var tex = AddZip(add, file_type == ".tpf");

            if (add) tex.Loaded = true;
            return tex;
        }

        public int SetFile(string file) { FileName = file; Loaded = false; return 0; }
        public string GetFile() { return FileName; }

        private int ReadFile()
        {
            if (Loaded) return 0;
            XORed = false;

            var dat = new FileStream(FileName, FileMode.Open);
            FileLen = dat.Length;

            FileInMemory = new byte[FileLen + 1];

            var result = dat.Read(FileInMemory, 0, (int)FileLen);
            dat.Close();

            if (result != FileLen)
            {
                return -1;
            }
            FileInMemory[FileLen] = 0;

            Loaded = true;
            return 0;
        }

        private int UnXOR()
        {
            if (XORed) return 0;
            var size = FileLen / 4u;
            var buff = new uint[size];
            System.Buffer.BlockCopy(FileInMemory, 0, buff, 0, FileInMemory.Length);
            var TPF_XOR = 0x3FA43FA4u;
            for (var i = 0; i < size; i++) buff[i] ^= TPF_XOR;

            System.Buffer.BlockCopy(buff, 0, FileInMemory, 0, FileInMemory.Length);

            for (var i = size * 4; i < size * 4 + FileLen % 4u; i++)
            {
                FileInMemory[i] ^= (byte)TPF_XOR;
            }

            var pos = FileLen - 1;
            while (pos > 0u && FileInMemory[pos] != 0) pos--;
            if (pos > 0u && pos < FileLen - 1) FileLen = pos + 1;
            XORed = true;

            return 0;
        }

        private int GetCommentZip(string tool_tip)
        {
            return 0;
        }

        private int GetCommentTpf(string tool_tip)
        {
            return 0;
        }

        private AddTextureClass AddZip(bool add, bool tpf)
        {
            int ret = ReadFile();
            if (ret != 0) return null;

            if (tpf)
            {
                UnXOR();

                var pw = new byte[] {0x73, 0x2A, 0x63, 0x7D, 0x5F, 0x0A, 0xA6, 0xBD,
                    0x7D, 0x65, 0x7E, 0x67, 0x61, 0x2A, 0x7F, 0x7F,
                    0x74, 0x61, 0x67, 0x5B, 0x60, 0x70, 0x45, 0x74,
                    0x5C, 0x22, 0x74, 0x5D, 0x6E, 0x6A, 0x73, 0x41,
                    0x77, 0x6E, 0x46, 0x47, 0x77, 0x49, 0x0C, 0x4B,
                    0x46, 0x6F, 0};

                return AddContent(pw, add);
            }
            else
            {
                return AddContent(null, add);
            }
        }

        private AddTextureClass AddContent(byte[] pw, bool add)
        {
            var tex = new AddTextureClass();
            using (var archive = new ZipFile(FileName, Encoding.Default))
            {
                archive.Password = Encoding.Default.GetString(pw);
                archive.Encryption = EncryptionAlgorithm.PkzipWeak; // the default: you might need to select the proper value here
                archive.StatusMessageTextWriter = Console.Out;

                var contents = new Dictionary<string, byte[]>();
                foreach (var entry in archive.Entries)
                {
                    var content = new byte[entry.UncompressedSize];
                    entry.Extract(new MemoryStream(content));
                    contents[entry.FileName] = content;
                }

                var texmoddef = contents["texmod.def"];
                var texstring = Encoding.Default.GetString(texmoddef);
                var lines = texstring.Split('\n');
                var count = 0;
                foreach (var line in lines)
                {
                    var splits = new List<string>(line.Replace("\r", "").Split('|'));
                    var addrstr = splits.First();
                    var addr = Convert.ToUInt64(addrstr.Replace("0x", ""), 16);
                    splits.RemoveAt(0);
                    var path = string.Join("|", splits);
                    while (path[0] == '.' && (path[1] == '/' || path[1] == '\\') || path[0] == '/' || path[0] == '\\') path = path.Remove(0, 1);

                    if (!add)
                    {
                        tex.Hash[count] = addr;
                        tex.Size[count] = 0;
                        count++;
                        continue;
                    }

                    if (!contents.ContainsKey(path)) continue;
                    var item = contents[path];
                    tex.Textures[count] = new List<byte>(item);
                    tex.Hash[count] = addr;
                    tex.Size[count] = (uint)item.Length;
                    count++;
                }
            }

            return null;
        }

        private string FileName;
        private bool Loaded;
        private bool XORed;
        private byte[] FileInMemory;
        private long FileLen;

    }
}
