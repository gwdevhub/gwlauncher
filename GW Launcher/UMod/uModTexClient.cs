using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.VisualBasic;

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
    public uint value;
    public uint hash;
}

public class uModTexClient
{
    private const int SMALL_PIPE_SIZE = 1 << 10;
    private const int BIG_PIPE_SIZE = 1 << 24;
    private readonly NamedPipeServerStream _pipeReceive;
    private readonly NamedPipeServerStream _pipeSend;

    private readonly List<TexBundle> _bundles;
    private readonly Queue<byte[]> _packets = new();
    private readonly HashSet<uint> _hashes = new();

    private bool _disposed;

    public uModTexClient()
    {
        var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var account = (NTAccount)sid.Translate(typeof(NTAccount));
        var rule = new PipeAccessRule(
            account.ToString(),
            PipeAccessRights.FullControl,
            AccessControlType.Allow);
        var securityPipe = new PipeSecurity();
        securityPipe.AddAccessRule(rule);

        _pipeReceive = NamedPipeServerStreamAcl.Create(
            "Game2uMod", PipeDirection.In,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.None, SMALL_PIPE_SIZE, SMALL_PIPE_SIZE, securityPipe);

        _pipeSend = NamedPipeServerStreamAcl.Create(
            "uMod2Game", PipeDirection.Out,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte, PipeOptions.None, BIG_PIPE_SIZE, BIG_PIPE_SIZE, securityPipe);

        _disposed = false;
        _pipeReceive.BeginWaitForConnection((IAsyncResult eAr) =>
        {
            var buf = new byte[SMALL_PIPE_SIZE];
            var num = _pipeReceive.Read(buf);
            if (num <= 2) return;
            // ReSharper disable once UnusedVariable
            var gameName = Encoding.Default.GetString(buf);

            if (!_pipeSend.IsConnected)
            {
                _pipeSend.BeginWaitForConnection((IAsyncResult iAr) =>
                {

                }, null);
            }
        }, null);
        _bundles = new List<TexBundle>();
    }
    ~uModTexClient()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (!Send()) return;
        _bundles.Clear();
        _packets.Clear();
        _pipeSend.Dispose();
        _pipeReceive.Dispose();
        _disposed = true;
    }

    public void AddFile(string filePath)
    {
        var bundle = new TexBundle(filePath);
        _bundles.Add(bundle);
    }

    public bool Send()
    {
        foreach (var tex in _bundles.SelectMany(bundle => bundle.defs))
        {
            if (_hashes.Contains(tex.crcHash)) continue; // do not send previously loaded textures
            if (_packets.Select(l => l.Length).Sum() + 2 * Marshal.SizeOf(typeof(Msg)) + sizeof(uint) + tex.fileData.Length > BIG_PIPE_SIZE)
            {
                var loadmoreMsg = new Msg
                {
                    hash = 0,
                    msg = MsgControl.CONTROL_MORE_TEXTURES,
                    value = 0
                };
                AddMessage(loadmoreMsg, Array.Empty<byte>());
                if (!SendAll())
                {
                    MessageBox.Show(@"Failed to send textures");
                }
            }
            var msg = new Msg
            {
                hash = tex.crcHash,
                msg = MsgControl.CONTROL_FORCE_RELOAD_TEXTURE_DATA,
                value = (uint)tex.fileData.Length
            };
            _hashes.Add(tex.crcHash);
            AddMessage(msg, tex.fileData);
        }

        var success = SendAll();
        if (success)
        {
            _bundles.Clear();
        }

        return success;
    }

    private void AddMessage(Msg msg, byte[] data)
    {
        var packet = new byte[12 + data.Length];

        var buf = BitConverter.GetBytes((uint)msg.msg);
        buf.CopyTo(packet, 0);
        buf = BitConverter.GetBytes(msg.value);
        buf.CopyTo(packet, 4);
        buf = BitConverter.GetBytes(msg.hash);
        buf.CopyTo(packet, 8);

        data.CopyTo(packet, 12);

        _packets.Enqueue(packet);
    }

    private bool SendAll()
    {
        if (!_pipeSend.IsConnected || !_pipeSend.CanWrite) return false;

        var buffer = _packets.SelectMany(b => b).ToArray();
        _pipeSend.Write(buffer, 0, buffer.Length);
        _packets.Clear();
        
        // TODO: this should work... find out why it doesn't and possibly fix umods d3d9.dll
        //while (_packets.Any())
        //{
        //    var buffer = _packets.Dequeue();
        //    _pipeSend.Write(buffer, 0, buffer.Length);
        //}

        return !_packets.Any();

    }
}