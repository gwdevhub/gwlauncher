using System;
using System.IO;

namespace UmodServer
{
    public class TpfLoader : System.IO.FileStream
    {
        bool isCrypted;


        public TpfLoader(string fileName, bool isCrypted)
            : base(fileName, FileMode.Open)
        {
            this.isCrypted = isCrypted;
        }

        private byte[] TPFCrypt(byte[] file)
        {
            const UInt32 xorkey = 0x3FA43FA4;
            byte[] xorbytes = BitConverter.GetBytes(xorkey);
            int intLen = file.Length / 4;

            for(int i = 0; i < intLen; i++)
            {
                UInt32 lol = BitConverter.ToUInt32(file, i * 4);
                lol ^= xorkey;
                file.SetValue(lol, i * 4);
            }

            for(int i = 0; i < file.Length; i++)
            {
                file[i] ^= xorbytes[0];
            }

            return file;
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int data = base.Read(array, offset, count);

            return data;
        }
    }
}
