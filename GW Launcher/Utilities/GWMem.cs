using System;
using GWCA.Memory;

namespace GW_Launcher
{
    class GWMem
    {
        public static IntPtr WinTitle = IntPtr.Zero;
        public static IntPtr EmailAddPtr = IntPtr.Zero;
        public static IntPtr CharnamePtr = IntPtr.Zero;
       

        public static void FindAddressesIfNeeded(GWCAMemory cli)
        {
            IntPtr tmp;
            Tuple<IntPtr, int> imagebase = cli.GetImageBase();
            cli.InitScanner(imagebase.Item1, imagebase.Item2);
            tmp = cli.ScanForPtr(new byte[] { 0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x10, 0x56, 0x6A }, 0x22, true);
            if (tmp != IntPtr.Zero)
            {
                WinTitle = tmp;
            }
            tmp = cli.ScanForPtr(new byte[] { 0x83, 0xC4, 0x40, 0x5F, 0x5E, 0x5B, 0x8B, 0x4D });
            if (tmp != IntPtr.Zero)
            {
                EmailAddPtr = cli.Read<IntPtr>(new IntPtr(tmp.ToInt32() - 0x48));
                CharnamePtr = cli.Read<IntPtr>(new IntPtr(tmp.ToInt32() - 0x2E));
            }
            cli.TerminateScanner();
        }
    }
}
