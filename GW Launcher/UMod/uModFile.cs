namespace GW_Launcher.uMod;

public class uModFile
{
    public uModFile(string fileName)
    {
        FileName = fileName;
    }

    public bool FileSupported()
    {
        return FileName.EndsWith(".tpf") || FileName.EndsWith(".zip");
    }

    private bool ReadFile()
    {
        if (Loaded) return true;
        XORed = false;

        var dat = new FileStream(FileName, FileMode.Open);
        FileLen = dat.Length;

        FileInMemory = new byte[FileLen];

        var result = dat.Read(FileInMemory, 0, (int)FileLen);
        dat.Close();

        if (result != FileLen)
        {
            return false;
        }

        Loaded = true;
        return true;
    }

    private void UnXOR()
    {
        if (XORed) return;
        var size = FileLen / 4u;
        var buff = new uint[size];
        for (var i = 0; i < size; i++)
        {
            buff[i] = BitConverter.ToUInt32(FileInMemory, i * 4);
        }

        var TPF_XOR = 0x3FA43FA4u;
        for (var i = 0; i < size; i++) buff[i] ^= TPF_XOR;

        for (var i = 0; i < size; i++)
        {
            var arr = BitConverter.GetBytes(buff[i]);
            for (var j = 0; j < arr.Length; j++)
            {
                FileInMemory[i*4 + j] = arr[j];
            }
        }

        var xorbytes = BitConverter.GetBytes(TPF_XOR);
        for (var i = size * 4; i < size * 4 + FileLen % 4u; i++)
        {
            FileInMemory[i] ^= xorbytes[0];
        }

        var pos = (int)FileLen - 1;
        while (pos > 0u && FileInMemory[pos] != 0) pos--;
        if (pos > 0u && pos < FileLen - 1) FileLen = pos + 1;
        var buf = new byte[FileLen];
        Buffer.BlockCopy(FileInMemory, 0, buf, 0, (int) FileLen);
        FileInMemory = new byte[FileLen];
        Buffer.BlockCopy(buf, 0, FileInMemory, 0, (int)FileLen);
        XORed = true;
    }


    public byte[]? GetFile()
    {
        if (Loaded) return FileInMemory;
        var ret = ReadFile();
        if (!ret) return null;

        if (FileName.EndsWith(".tpf"))
        {
            UnXOR();
        }
        return FileInMemory;
    }

    private string FileName { get; set; }
    private bool Loaded { get; set; }
    private bool XORed { get; set; }
    private byte[] FileInMemory { get; set; }
    private long FileLen { get; set; }

}