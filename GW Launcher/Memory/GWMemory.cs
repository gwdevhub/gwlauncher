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

        // Signatures mirror the GWToolbox launcher (GWToolbox/Inject.cpp); offset lands on the pushed pointer.
        EmailAddPtr =
            cli.ScanForPtr(new byte[] { 0x68, 0x80, 0x00, 0x00, 0x00, 0x51, 0x68 }, 0x7, true);

        CharnamePtr =
            cli.ScanForPtr(new byte[] { 0x6A, 0x14, 0x83, 0xC0, 0x18, 0x50, 0x68 }, 0x7, true);

        cli.TerminateScanner();
    }
}
