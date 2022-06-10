namespace GW_Launcher.uMod;

public class uModFile : IDisposable
{
    private bool _disposed;

    public uModFile(string fileName)
    {
        FileName = fileName;
        FileInMemory = Array.Empty<byte>();
    }

    private string FileName { get; }
    private bool Loaded { get; set; }
    private bool XORed { get; set; }
    private byte[] FileInMemory { get; set; }
    private long FileLen { get; set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        FileInMemory = Array.Empty<byte>();
        FileLen = 0;
        Loaded = false;
        _disposed = true;
    }

    private bool ReadFile()
    {
        if (Loaded)
        {
            return true;
        }

        if (_disposed)
        {
            return false;
        }

        XORed = false;

        using var dat = new FileStream(FileName, FileMode.Open);
        FileLen = dat.Length;

        FileInMemory = new byte[FileLen];

        var result = dat.Read(FileInMemory, 0, (int) FileLen);

        if (result != FileLen)
        {
            return false;
        }

        Loaded = true;
        return true;
    }

    private void UnXOR()
    {
        if (XORed)
        {
            return;
        }

        if (_disposed)
        {
            return;
        }

        var size = FileLen / 4u;
        var buff = new uint[size];
        for (var i = 0; i < size; i++)
        {
            buff[i] = BitConverter.ToUInt32(FileInMemory, i * 4);
        }

        var TPF_XOR = 0x3FA43FA4u;
        for (var i = 0; i < size; i++)
        {
            buff[i] ^= TPF_XOR;
        }

        for (var i = 0; i < size; i++)
        {
            var arr = BitConverter.GetBytes(buff[i]);
            for (var j = 0; j < arr.Length; j++)
            {
                FileInMemory[i * 4 + j] = arr[j];
            }
        }

        var xorbytes = BitConverter.GetBytes(TPF_XOR);
        for (var i = size * 4; i < size * 4 + FileLen % 4u; i++)
        {
            FileInMemory[i] ^= xorbytes[0];
        }

        var pos = (int) FileLen - 1;
        while (pos > 0u && FileInMemory[pos] != 0)
        {
            pos--;
        }

        if (pos > 0u && pos < FileLen - 1)
        {
            FileLen = pos + 1;
        }

        XORed = true;
    }

    public byte[]? GetContent()
    {
        if (_disposed)
        {
            return null;
        }

        if (Loaded)
        {
            return FileInMemory[..(Index) FileLen];
        }

        var ret = ReadFile();
        if (!ret)
        {
            return null;
        }

        if (FileName.EndsWith(".tpf"))
        {
            UnXOR();
        }

        return FileInMemory[..(Index) FileLen];
    }
}
