using System;
using System.Collections.Generic;
using System.Linq;

namespace UmodServer
{
    public struct TexDef
    {
        public UInt32 crcHash;
        public string fileName;
        public byte[] fileData;
    }
    public class TexBundle
    {
        public string name;
        public List<TexDef> defs;

        public TexBundle(string filePath)
        {
            this.name = filePath.Split('\\').Last();

            using (ZipLoader loader = new ZipLoader(filePath))
            {
                foreach(var entry in loader.Entries)
                {
                    string[] tmp = entry.Name.Split('_', '.');

                    if (tmp.Length != 3)
                        throw new Exception("Not using a texmod created texture :s");

                   // string exeName = tmp[0];
                    string crc = tmp[1];
                   // string textureType = tmp[2];

                    TexDef def;
                    def.fileName = entry.Name;
                    def.crcHash = UInt32.Parse(crc);


                    using (var file = entry.Open())
                    {
                        def.fileData = new byte[entry.Length];
                        file.Read(def.fileData, 0, (int)entry.Length);
                    }

                    this.defs.Add(def);
                }
            }
        }

        ~TexBundle()
        {

        }
    }
}
