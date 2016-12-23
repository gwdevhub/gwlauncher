using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GW_Launcher
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// 
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        extern static IntPtr FindWindowW(string clsname, string winname);

        [DllImport("user32.dll")]
        extern static bool SetForegroundWindow(IntPtr hWNd);

        [STAThread]
        static void Main()
        {
            IntPtr win = FindWindowW(null, "GW Launcher");
            if (win != IntPtr.Zero)
            {
                SetForegroundWindow(win);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm()); 
        }
    }
}
