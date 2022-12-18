using System;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace GW_Launcher.Utilities;

sealed class NotifyIconHelper
{

    public static Rectangle GetIconRect(NotifyIcon icon)
    {
        RECT rect = new RECT();
        NOTIFYICONIDENTIFIER notifyIcon = new NOTIFYICONIDENTIFIER();

        notifyIcon.cbSize = Marshal.SizeOf(notifyIcon);
        //use hWnd and id of NotifyIcon instead of guid is needed
        notifyIcon.hWnd = GetHandle(icon);
        notifyIcon.uID = GetId(icon);

        int hresult = Shell_NotifyIconGetRect(ref notifyIcon, out rect);
        //rect now has the position and size of icon

        return new Rectangle(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public readonly Int32 left;
        public readonly Int32 top;
        public readonly Int32 right;
        public readonly Int32 bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONIDENTIFIER
    {
        public Int32 cbSize;
        public IntPtr hWnd;
        public Int32 uID;
        public readonly Guid guidItem;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int Shell_NotifyIconGetRect([In] ref NOTIFYICONIDENTIFIER identifier, [Out] out RECT iconLocation);

    private static readonly FieldInfo windowField = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
    private static IntPtr GetHandle(NotifyIcon icon)
    {
        if (windowField == null) throw new InvalidOperationException("[Useful error message]");
        NativeWindow window = windowField.GetValue(icon) as NativeWindow;

        if (window == null) throw new InvalidOperationException("[Useful error message]");  // should not happen?
        return window.Handle;
    }

    private static readonly FieldInfo idField = typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
    private static int GetId(NotifyIcon icon)
    {
        if (idField == null) throw new InvalidOperationException("[Useful error message]");
        return Convert.ToInt32(idField.GetValue(icon));
    }

}
