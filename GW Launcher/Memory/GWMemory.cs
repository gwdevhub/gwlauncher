namespace GW_Launcher.Memory;

internal class GWMemory
{
    public static IntPtr WinTitle = IntPtr.Zero;
    public static IntPtr EmailAddPtr = IntPtr.Zero;
    public static IntPtr CharnamePtr = IntPtr.Zero;

    internal static void FindAddressesIfNeeded(GWCAMemory cli)
    {
        var imagebase = cli.GetImageBase();
        cli.InitScanner(imagebase.Item1, imagebase.Item2);

        WinTitle = cli.ScanForPtr(new byte[] { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x10, 0x56, 0x6A }, 0x22, true);

        EmailAddPtr =
            cli.ScanForPtr(new byte[] { 0x33, 0xC0, 0x5D, 0xC2, 0x10, 0x00, 0xCC, 0x68, 0x80, 0x00, 0x00, 0x00 }, 0xE,
                true);

        CharnamePtr =
            cli.ScanForPtr(new byte[] { 0x8B, 0xF8, 0x6A, 0x03, 0x68, 0x0F, 0x00, 0x00, 0xC0, 0x8B, 0xCF, 0xE8 }, -0x42,
                true);

        cli.TerminateScanner();
    }
}
