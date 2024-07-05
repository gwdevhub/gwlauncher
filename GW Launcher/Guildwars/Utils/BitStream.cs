/// <summary>
/// https://github.com/reduf/Headquarter/blob/decomp/tools/inflate.py
/// </summary>
internal sealed class BitStream
{
    private readonly Stream input;
    private uint buf1;
    private uint buf2;
    private int idx;
    private int avail;

    public BitStream(Stream input)
    {
        if (!input.CanRead)
        {
            throw new ArgumentException("Input must be readable stream");
        }

        if (input.Length - input.Position < 8)
        {
            throw new ArgumentException("Input length must be at least 8");
        }

        this.input = input.ThrowIfNull();
        var tempBuffer = new byte[8];
        input.ReadAtLeast(tempBuffer, 8);
        this.buf1 = BitConverter.ToUInt32(tempBuffer, 0);
        this.buf2 = BitConverter.ToUInt32(tempBuffer, 4);
        this.idx = 8;
        this.avail = 32;
    }

    public uint Peek(int count)
    {
        if (count > 32)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be less than or equal to 32");

        return this.buf1 >> (32 - count);
    }

    public uint Read(int count)
    {
        uint result = this.Peek(count);
        this.Consume(count);
        return result;
    }

    public void Consume(int count)
    {
        this.buf1 = (this.buf2 >> (32 - count)) | U32(this.buf1 << count);

        if (this.avail < count)
        {
            if (this.idx >= this.input.Length)
            {
                this.avail = 0;
                this.buf2 = 0;
            }
            else
            {
                var bytes = new byte[4];
                this.input.Read(bytes, 0, 4);
                this.buf2 = BitConverter.ToUInt32(bytes);
                this.idx += 4;
                var newAvail = (this.avail + 32) - count;
                this.buf1 += this.buf2 >> newAvail;
                this.buf2 = U32(this.buf2 << (count - this.avail));
                this.avail = newAvail;
            }
        }
        else
        {
            this.avail -= count;
            this.buf2 = U32(this.buf2 << count);
        }
    }

    private static uint U32(uint v)
    {
        return v & 0xFFFFFFFF;
    }
}
