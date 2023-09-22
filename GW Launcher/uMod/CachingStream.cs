namespace GW_Launcher.uMod;

/// <summary>
///     Stream that caches the contents until <see cref="Flush" /> is called.
/// </summary>
internal sealed class CachingStream : Stream
{
    private readonly Stream innerStream;
    private long cachedContentLength;
    private byte[] innerBuffer;

    public CachingStream(Stream innerStream, int cacheSize = 1024)
    {
        this.innerStream = innerStream;
        if (!innerStream.CanWrite)
        {
            throw new ArgumentException($"Provided stream must have {nameof(this.innerStream.CanWrite)} set to true");
        }

        innerBuffer = new byte[cacheSize];
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => cachedContentLength;
    public override long Position { get; set; }

    public override void Flush()
    {
        innerStream.Write(innerBuffer, 0, (int)cachedContentLength);
        cachedContentLength = 0;
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await innerStream.WriteAsync(innerBuffer, 0, (int)cachedContentLength, cancellationToken);
        cachedContentLength = 0;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (cachedContentLength + count > innerBuffer.Length)
        {
            var newBuffer = new byte[innerBuffer.Length * 2];
            Array.Copy(innerBuffer, newBuffer, count);
            innerBuffer = newBuffer;
        }

        Array.Copy(buffer, offset, innerBuffer, cachedContentLength, count);
        cachedContentLength += count;
    }

    public override void Close()
    {
        innerBuffer = null!;
        innerStream?.Dispose();
        base.Close();
    }
}
