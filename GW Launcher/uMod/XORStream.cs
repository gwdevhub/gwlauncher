using System.Runtime.CompilerServices;

namespace GW_Launcher.uMod;

internal sealed class XORStream : Stream
{
    private const byte TPF_XOROdd = 0x3F;
    private const byte TPF_XOREven = 0xA4;

    private readonly Stream innerStream;
    private readonly long originalStreamLength;

    public XORStream(Stream stream)
    {
        if (!stream.CanSeek ||
            !stream.CanRead)
        {
            throw new ArgumentException(
                $"Provided stream needs to have SEEK set to True (actual value: {stream.CanSeek}), and READ set to true (actual value: {stream.CanRead})");
        }

        innerStream = stream;
        originalStreamLength = innerStream.Length;
        innerStream.Position = originalStreamLength - 1;
        while (innerStream.Position > 0)
        {
            /*
             * Read the byte through this stream's algorithm, XOR-ing the read byte
             */
            var readByte = ReadByte();
            if (readByte == 0)
            {
                break;
            }

            innerStream.Position -= 2; //-1 for the ReadByte and -1 to go one position in the back
        }

        var lastZeroPosition = innerStream.Position - 1;
        innerStream.Position = 0;
        Length = lastZeroPosition;
    }

    public override bool CanRead => innerStream.CanRead;
    public override bool CanSeek => innerStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position
    {
        get => Math.Min(innerStream.Position, Length);
        set => innerStream.Position = value;
    }

    public override void Flush()
    {
        innerStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        /*
         * The file contains bytes in groups of 2.
         * Each group needs to be XORed with 0x3FA4.
         * In x86, uint is little-endian, which would mean that
         * a short value is stored as LSB first and MSB after.
         *
         * That means that we need to read the bytes from the inner stream
         * and XOR the bytes at even position with 0xA4 and XOR the bytes
         * at the odd position with 0x3F.
         *
         * We also need to keep track of the current position in the inner stream
         * to figure out if the bytes we are reading start from an odd or even position.
         */

        var read = innerStream.Read(buffer, offset, count);

        /*
         * In order to figure out the starting position of the bytes we have read,
         * we can first let the inner stream read what it wants, then we can simply
         * deduct the number of read bytes from the final position in the stream.
         */
        var startPosition = innerStream.Position - read;
        for (var i = 0; i < read; i++)
        {
            buffer[i + offset] = XOR(buffer[i], i + startPosition);
        }

        /*
         * All bytes read over the innerStreamLength should be zeroed out and ignored.
         * This happens only when reaching the end of the stream, otherwise bytesOverLength will be negative
         * thus skipping the loop and returning read - 0.
         */
        var bytesOverLength = startPosition + read - Length;
        for (var i = 0; i < bytesOverLength; i++)
        {
            if (offset + read - i - 1 >= buffer.Length)
            {
                continue;
            }

            if (offset + read - i - 1 < 0)
            {
                break;
            }

            buffer[offset + read - i - 1] = 0;
        }

        return Math.Max(read, read - (int)Math.Max(0, bytesOverLength));
    }

    public override int ReadByte()
    {
        var bytePos = innerStream.Position;
        var b = (byte)innerStream.ReadByte();
        return XOR(b, bytePos);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte XOR(byte b, long position)
    {
        /*
         * If the index of the inner stream is even, XOR with 0xA4
         * Otherwise XOR with 0x3F.
         *
         * Edge case when the byte is in the last 3 positions,
         * it needs to be XORed with 0x3F.
         */
        if (position > originalStreamLength - 4)
        {
            return (byte)(b ^ TPF_XOREven);
        }

        return position % 2 == 0 ? (byte)(b ^ TPF_XOREven) : (byte)(b ^ TPF_XOROdd);
    }
}
