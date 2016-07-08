using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GW_Launcher
{
    class GWMem
    {
        // These will probly change if there is an update :P
        public static IntPtr WinTitle    = new IntPtr(0x009CDB10);
        public static IntPtr EmailAddPtr = new IntPtr(0x00A2AFC0);
        public static IntPtr CharnamePtr = new IntPtr(0x00A2AD88);
    }
}
