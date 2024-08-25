using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GW_Launcher.Utilities;
internal static class ScreenScaling
{
    [DllImport("gdi32.dll")]
    static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    private enum DeviceCap
    {
        VERTRES = 10,
        DESKTOPVERTRES = 117,
        //... http://pinvoke.net/default.aspx/gdi32/GetDeviceCaps.html
    }

    //get screen scaling factor set under settings -> system -> screen: scaling (e.g. 225%)
    public static float GetScreenScalingFactor()
    {
        using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
        IntPtr desktop = g.GetHdc();

        int logicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.VERTRES); //virtual screen resolution scaled down for DPI-unaware app
        int physicalScreenHeight = GetDeviceCaps(desktop, (int)DeviceCap.DESKTOPVERTRES); //actual screen resolution, e.g. 3840 x 2160

        g.ReleaseHdc(desktop);

        float screenScalingFactor = physicalScreenHeight / (float) logicalScreenHeight;
        return screenScalingFactor; // e.g. 1.25 = 125%
    }
}
