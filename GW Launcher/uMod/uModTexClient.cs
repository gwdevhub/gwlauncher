using System.Extensions;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace GW_Launcher.uMod;

internal sealed class uModTexClient : IDisposable
{
    private const int SMALL_PIPE_SIZE = 1 << 10;
    private const int BIG_PIPE_SIZE = 1 << 24;
    private readonly HashSet<uint> hashes = new();

    private readonly List<ZipLoader> texturePackLoaders = new();
    private CachingStream? cachingStream;

    private NamedPipeServerStream? pipeReceive;
    private NamedPipeServerStream? pipeSend;
    private bool receiveConnected;
    private bool sendConnected;

    public bool Ready => receiveConnected && sendConnected;

    public void Dispose()
    {
        CloseConnection();
    }

    public async void Initialize(CancellationToken cancellationToken)
    {
        var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var account = sid.Translate(typeof(NTAccount)).As<NTAccount>();
        var rule = new PipeAccessRule(account?.ToString()!, PipeAccessRights.FullControl, AccessControlType.Allow);
        var securityPipe = new PipeSecurity();
        securityPipe.AddAccessRule(rule);

        pipeReceive = NamedPipeServerStreamAcl.Create(
            "Game2uMod",
            PipeDirection.In,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.None,
            SMALL_PIPE_SIZE,
            SMALL_PIPE_SIZE,
            securityPipe);

        pipeSend = NamedPipeServerStreamAcl.Create(
            "uMod2Game",
            PipeDirection.Out,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.None,
            BIG_PIPE_SIZE,
            BIG_PIPE_SIZE,
            securityPipe);

        await BeginReceive(cancellationToken);
    }

    public async Task AddFile(string filePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(filePath);
        var texLoader = new ZipLoader(fullPath);
        try
        {
            await texLoader.LoadAsync(cancellationToken);
            texturePackLoaders.Add(texLoader);
        }
        catch (Exception)
        {
        }
    }

    public async Task Send(CancellationToken cancellationToken)
    {
        while (!Ready)
        {
            await Task.Delay(100, cancellationToken);
        }

        foreach (var loader in texturePackLoaders)
        {
            foreach (var entry in await loader.LoadAsync(cancellationToken))
            {
                if (hashes.Contains(entry.CrcHash))
                {
                    continue;
                }

                if (cachingStream!.Length + 2 * Marshal.SizeOf(typeof(TexmodMessage)) + entry.Entry!.UncompressedSize >
                    BIG_PIPE_SIZE)
                {
                    var loadMore = new TexmodMessage(ControlMessage.CONTROL_MORE_TEXTURES, 0, 0);
                    AddMessage(loadMore, default);
                    if (!await SendAll(cancellationToken))
                    {
                        break;
                    }
                }

                await using var reader = entry.Entry.OpenReader();
                var msg = new TexmodMessage(ControlMessage.CONTROL_ADD_TEXTURE_DATA, (uint)reader.Length,
                    entry.CrcHash);
                hashes.Add(entry.CrcHash);
                AddMessage(msg, reader);
            }
        }

        var success = await SendAll(cancellationToken);
        if (!success)
        {
            return;
        }

        foreach (var loader in texturePackLoaders)
        {
            loader.Dispose();
        }

        texturePackLoaders.Clear();
    }

    private void CloseConnection()
    {
        receiveConnected = false;
        sendConnected = false;
        cachingStream?.Dispose();
        cachingStream = null;
        pipeReceive?.Dispose();
        pipeReceive = null;
        pipeSend?.Dispose();
        pipeSend = null;
        foreach (var loader in texturePackLoaders)
        {
            loader.Dispose();
        }

        texturePackLoaders.Clear();
        hashes.Clear();
    }

    private async Task<bool> SendAll(CancellationToken cancellationToken)
    {
        while (pipeSend?.IsConnected is not true ||
               pipeSend?.CanWrite is not true ||
               Ready is not true)
        {
            await Task.Delay(100, cancellationToken);
        }

        await cachingStream!.FlushAsync(cancellationToken);
        return true;
    }

    private void AddMessage(TexmodMessage msg, Stream? data)
    {
        cachingStream!.Write(BitConverter.GetBytes((uint)msg.Message));
        cachingStream!.Write(BitConverter.GetBytes(msg.Value));
        cachingStream!.Write(BitConverter.GetBytes(msg.Hash));
        if (data is Stream stream)
        {
            stream.CopyTo(cachingStream!);
        }
    }

    private async Task BeginReceive(CancellationToken cancellationToken)
    {
        if (pipeReceive is null)
        {
            throw new InvalidOperationException("Unexpected error. Receive pipe is null");
        }

        if (pipeSend is null)
        {
            throw new InvalidOperationException("Unexpected error. Send pipe is null");
        }

        await pipeReceive.WaitForConnectionAsync(cancellationToken);

        var buf = new byte[SMALL_PIPE_SIZE];
        var num = await pipeReceive.ReadAsync(buf, cancellationToken);
        receiveConnected = true;

        if (num <= 2)
        {
            return;
        }

        Encoding.Unicode.GetString(buf).Replace("\0", "");
        if (!pipeSend.IsConnected)
        {
            await pipeSend.WaitForConnectionAsync(cancellationToken);
            cachingStream = new CachingStream(pipeSend, BIG_PIPE_SIZE);
            sendConnected = true;
        }
    }
}
