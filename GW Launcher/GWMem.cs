using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using GWCA.Memory;

namespace GW_Launcher
{
    class GWMem
    {
        public static bool scanned = false;
        // These will probly change if there is an update :P
        public static IntPtr WinTitle    = new IntPtr(0x009CDB10);
        public static IntPtr EmailAddPtr = new IntPtr(0x00A2AEBC);
        public static IntPtr CharnamePtr = new IntPtr(0x00A2AE80);
        //public static IntPtr DATInfo     = new IntPtr(0x00A35300);


        public static void FindAddresses(GWCAMemory cli)
        {
            IntPtr tmp;
            cli.InitScanner(new IntPtr(0x00401000), 0x0049A000);

            tmp = cli.ScanForPtr(new byte[] { 0x33, 0xD2, 0x8B, 0xCE, 0x57, 0x6A, 0x0C }, 0x0D, true);
            if (tmp != IntPtr.Zero)
            {
                WinTitle = tmp;
            }
               
            tmp = cli.ScanForPtr(new byte[] { 0x6A, 0x14, 0x8D, 0x96, 0xBC });
            if (tmp != IntPtr.Zero)
            {
                EmailAddPtr = new IntPtr(tmp.ToInt32() - 0x9);
                CharnamePtr = new IntPtr(tmp.ToInt32() + 0x9);
            }

            cli.FreeScanner();
            scanned = true;
        }

    }
}
