using System.Collections.Generic;

namespace UmodServer
{
    public class AddTextureClass
    {
        public AddTextureClass(AddTextureClass tex = null)
        {
            if (tex != null)
            {
                Hash = tex.Hash;
                WasAdded = tex.WasAdded;
                Size = tex.Size;
                Textures = tex.Textures;

                Len = tex.Len;
                Num = tex.Num;

                Add = tex.Add;
                Force = tex.Force;
                Loaded = tex.Loaded;

                File = tex.File;
                Comment = tex.Comment;
            }
            else
            {
                Num = 0;
                Textures = null;
                Size = null;
                Hash = null;
                WasAdded = null;
                Len = 0;

                Add = false;
                Force = false;
                Loaded = false;
            }
        }

        bool SetSize(uint num)
        {
            Num = 0;
            Textures = new List<byte>[num];
            Size = new uint[num];
            Hash = new ulong[num];
            WasAdded = new bool[num];
            Len = num;

            return true;
        }

        public uint Num { get; set; }
        public List<byte>[] Textures { get; set; }
        public uint[] Size { get; set; }
        public ulong[] Hash { get; set; }
        public bool[] WasAdded { get; set; }
        public uint Len { get; set; }

        public bool Add { get; set; }
        public bool Force { get; set; }
        public bool Loaded { get; set; }
        public string File { get; set; }
        public string Comment { get; set; }

	}
}
