using GW_Launcher.Guildwars.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GW_Launcher.Guildwars.Utils;
internal sealed class GuildwarsFileStream(
    GuildwarsClientContext guildwarsClientContext,
    GuildwarsClient guildwarsClient,
    int fileId,
    int sizeCompressed,
    int sizeDecompressed,
    int crc)
    : Stream
{
    private readonly GuildwarsClient guildwarsClient = guildwarsClient.ThrowIfNull();

    private byte[]? chunkBuffer;
    private int positionInBuffer = 0;
    private int chunkSize = 0;

    public int FileId { get; init; } = fileId;
    public int SizeCompressed { get; init; } = sizeCompressed;
    public int SizeDecompressed { get; init; } = sizeDecompressed;
    public int Crc { get; init; } = crc;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => this.SizeCompressed;
    public override long Position { get; set; }

    public override void Flush()
    {
        throw new System.NotImplementedException();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (this.Position >= this.Length)
        {
            return 0;
        }

        if (this.positionInBuffer < this.chunkSize)
        {
            var read = this.ReadCurrentChunkBytes(buffer, offset, count);
            this.Position += read;
            return read;
        }

        // If we have already requested a previous chunk, we need to request more data
        if (this.chunkSize > 0)
        {
            await this.guildwarsClient.Send(new FileRequestNextChunk { Field1 = 0x7F3, Field2 = 0x8, Field3 = (uint)this.chunkSize }, guildwarsClientContext, cancellationToken);
        }

        var meta = await this.guildwarsClient.ReceiveWait<FileMetadataResponse>(guildwarsClientContext, cancellationToken);
        if (meta.Field1 != 0x6F2 && meta.Field1 != 0x6F3)
        {
            throw new InvalidOperationException($"Unknown header in response {meta.Field1:X4}");
        }

        this.chunkSize = meta.Field2 - 4;
        if (this.chunkBuffer is null ||
            this.chunkBuffer.Length != this.chunkSize)
        {
            this.chunkBuffer = new byte[this.chunkSize];
        }

        var downloadedChunkSize = 0;
        do
        {
            var buf = new byte[Math.Min(4096, this.chunkSize - downloadedChunkSize)];
            var readTask = guildwarsClientContext.Socket.ReceiveAsync(buf, cancellationToken).AsTask();
            if (await Task.WhenAny(readTask, Task.Delay(5000, cancellationToken)) != readTask)
            {
                throw new TaskCanceledException("Timed out waiting for download");
            }

            var read = await readTask;
            Array.Copy(buf, 0, this.chunkBuffer, downloadedChunkSize, read);
            downloadedChunkSize += read;
        } while (downloadedChunkSize < this.chunkSize);

        this.positionInBuffer = 0;
        var chunkRead = this.ReadCurrentChunkBytes(buffer, offset, count);
        this.Position += chunkRead;
        return chunkRead;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return System.Extensions.TaskExtensions.RunSync(() => this.ReadAsync(buffer, offset, count));
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new System.NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new System.NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new System.NotImplementedException();
    }

    private int ReadCurrentChunkBytes(byte[] buffer, int offset, int count)
    {
        if (this.chunkBuffer is null)
        {
            throw new InvalidOperationException("No chunk buffer ready");
        }

        var bytesToRead = Math.Min(count, this.chunkSize - this.positionInBuffer);
        Array.Copy(this.chunkBuffer, this.positionInBuffer, buffer, offset, bytesToRead);
        this.positionInBuffer += bytesToRead;
        return bytesToRead;
    }
}
