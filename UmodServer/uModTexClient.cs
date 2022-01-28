using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Pipes;

namespace UmodServer
{
    public enum MsgControl : UInt32
    {
        CONTROL_ADD_TEXTURE = 1,
        CONTROL_FORCE_RELOAD_TEXTURE = 2,
        CONTROL_REMOVE_TEXTURE = 3,
        CONTROL_FORCE_RELOAD_TEXTURE_DATA = 4,
        CONTROL_ADD_TEXTURE_DATA = 5,
        CONTROL_MORE_TEXTURES = 6,
        CONTROL_END_TEXTURES = 7,

        CONTROL_SAVE_ALL = 10,
        CONTROL_SAVE_SINGLE = 11,
        CONTROL_SHOW_STRING = 12,
        CONTROL_SHOW_TEXTURE = 13,
        CONTROL_SET_DIR = 14,
        CONTROL_KEY_BACK = 20,
        CONTROL_KEY_SAVE = 21,
        CONTROL_KEY_NEXT = 22,
        CONTROL_FONT_COLOUR = 30,
        CONTROL_TEXTURE_COLOUR = 31,
        CONTROL_WIDTH_FILTER = 40,
        CONTROL_HEIGHT_FILTER = 41,
        CONTROL_DEPTH_FILTER = 42,
        CONTROL_FORMAT_FILTER = 43,
        CONTROL_SAVE_FORMAT = 50,
        CONTROL_SUPPORT_TPF = 60,
        CONTROL_GAME_EXIT = 100,
        CONTROL_ADD_CLIENT = 101,
        CONTROL_REMOVE_CLIENT = 102
    }
    
    public struct Msg
    {
        public MsgControl msg;
        public ulong value;
        public ulong hash;
    }

    public class uModTexClient
    {
        private NamedPipeClientStream pipeClient;

        public List<TexBundle> bundles;
        public List<TexDef> looseTextures;

        public uModTexClient()
        {
            pipeClient = new NamedPipeClientStream(".", "uMod2Game", PipeDirection.Out);
        }


        public void AddBundle(TexBundle bundle)
        {
            bundles.Add(bundle);
        }

        public void AddSingleFile(string texFilePath)
        {
            string fileName = texFilePath.Split('\\').Last();


            string[] tmp = fileName.Split('_', '.');

            if (tmp.Length != 3)
                throw new Exception("Not using a texmod created texture :s");

            // string exeName = tmp[0];
            string crc = tmp[1];
            // string textureType = tmp[2];

            TexDef def;
            def.fileName = fileName;
            def.crcHash = uint.Parse(crc);


            using (var file = new FileStream(texFilePath, FileMode.Open))
            {
                def.fileData = new byte[file.Length];
                file.Read(def.fileData, 0, (int)file.Length);
            }

            looseTextures.Add(def);
        }

        public void Send(MsgControl msg, ulong value, ulong hash, byte[] data = null)
        {
            byte[] packet = new byte[20 + (data != null ? data.Length : 0)];

            packet.SetValue(msg, 0);
            packet.SetValue(value, 4);
            packet.SetValue(hash, 12);
            packet.SetValue(data, 20);

            pipeClient.Write(packet, 0, packet.Length);
        }

        ~uModTexClient()
        {
            pipeClient.Close();
        }
    }
}
