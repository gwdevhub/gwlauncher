using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;

namespace GW_Launcher.uMod;

public enum MsgControl : uint
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
    private readonly NamedPipeServerStream pipeReceive;
    private readonly NamedPipeServerStream pipeSend;
    private const string PIPE_uMod2Game = "\\\\.\\pipe\\uMod2Game";
    private const string PIPE_Game2uMod = "\\\\.\\pipe\\Game2uMod";

    private List<TexBundle> bundles;
    private List<uModFile> files;
    private List<byte[]> bytes = new();

    public uModTexClient()
    {
        pipeReceive = new NamedPipeServerStream("Game2uMod", PipeDirection.In);
        pipeSend = new NamedPipeServerStream("uMod2Game", PipeDirection.Out);
        var res = pipeReceive.BeginWaitForConnection(async (IAsyncResult iar) =>
        {
            if (!pipeReceive.IsConnected)
            {
                pipeReceive.WaitForConnection();
            }
        }, null);
        bundles = new List<TexBundle>();
        files = new List<uModFile>();
    }
    
    internal void AddBundle(TexBundle bundle)
    {
        bundles.Add(bundle);
    }

    public void AddFile(string filePath)
    {
        var bundle = new TexBundle(filePath);
        bundles.Add(bundle);
    }

    public void Send()
    {
        foreach (var bundle in bundles)
        {
            foreach (var tex in bundle.defs)
            {
                var msg = new Msg();
                msg.hash = tex.crcHash;
                msg.msg = MsgControl.CONTROL_FORCE_RELOAD_TEXTURE_DATA;
                msg.value = (ulong) tex.fileData.Length;
                AddMessage(msg, tex.fileData);
            }
        }
        SendAll();
    }

    private void AddMessage(Msg msg, byte[] data)
    {
        var packet = new byte[20 + data.Length];

        var buf = BitConverter.GetBytes((uint) msg.msg);
        buf.CopyTo(packet, 0);
        buf = BitConverter.GetBytes(msg.value);
        buf.CopyTo(packet, 4);
        buf = BitConverter.GetBytes(msg.hash);
        buf.CopyTo(packet, 12);

        data.CopyTo(packet, 20);
        
        //packet.SetValue(msgbuf, 0);
        //packet.SetValue(msg.value, 4);
        //packet.SetValue(msg.hash, 12);
        //packet.SetValue(data, 20);
        bytes.Add(packet);
    }

    private void SendAll()
    {
        var buffer = bytes
            .SelectMany(a => a)
            .ToArray();

        if (pipeSend.IsConnected)
        {
            pipeSend.Write(buffer, 0, buffer.Length);
        }

    }

    ~uModTexClient()
    {
        pipeSend.Close();
    }
}