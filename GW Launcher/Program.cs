using Newtonsoft.Json;
using System.Runtime.InteropServices;
using GW_Launcher.Forms;
using GW_Launcher.Utilities;
using System.Diagnostics;

namespace GW_Launcher;
public class GlobalSettings
{
    public bool Encrypt { get; set; }

    GlobalSettings()
    {
        Encrypt = true;
    }

    public void Save(string path = "Settings.json")
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public static GlobalSettings Load(string path = "Settings.json")
    {
        try
        {
            var txt = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<GlobalSettings>(txt) ?? new GlobalSettings();
        }
        catch (FileNotFoundException)
        {
            var settings = new GlobalSettings();
            var result = MessageBox.Show("Would you like to encrypt the account info?", "Encryption", MessageBoxButtons.YesNo);
            if (result == DialogResult.No) { settings.Encrypt = false; }
            return settings;
        }

    }
}

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";
    public static AccountManager accounts;
    public static Thread mainthread;
    public static Mutex mutex = new();
    public static Mutex? gwlMutex;
    public static GlobalSettings settings = GlobalSettings.Load();

    [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hwnd, string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("uMod.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int RunServer();

    [DllImport("uMod.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool LoadTextures(string texturepack);

    [STAThread]
    internal static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (Mutex.TryOpenExisting(GwlMutexName, out gwlMutex))
        {
            return;
        }

        gwlMutex = new Mutex(true, GwlMutexName);

        var location = Path.GetDirectoryName(AppContext.BaseDirectory);
        if (location != null)
        {
            var filename = Path.Combine(location, "GWML.dll");
            File.WriteAllBytes(filename, Properties.Resources.GWML); //overwrite the file
        }

        try
        {
            accounts = new AccountManager("Accounts.json");
        }
        catch (Exception)
        {
            return;
        }
        foreach (var t in accounts)
        {
            t.active = false;
        }

        int server = RunServer();

        using var mf = new MainForm();
        mf.Location = new Point(-1000, -1000);
        mf.FormClosing += (sender, e) => { settings.Save(); };

        mainthread = new Thread(() =>
        {
            var mainClosed = false;
            mf.FormClosed += (sender, a) => { mainClosed = true; };
            while (!mainClosed)
            {
                while (mf.needtolaunch.Count > 0)
                {
                    var i = mf.needtolaunch.Dequeue();
                    var a = accounts[i];
                    if (a.active && a.process.process.MainWindowHandle != IntPtr.Zero)
                    {
                        SetForegroundWindow(a.process.process.MainWindowHandle);
                        continue;
                    }
                    var m = MulticlientPatch.LaunchClient(a.gwpath,
                        " -email \"" + a.email + "\" -password \"" + a.password + "\" -character \"" +
                        a.character + "\" " + a.extraargs, a.datfix, false, a.elevated, a.mods);

                    uint timelock = 0;
                    while (m.process.MainWindowHandle == IntPtr.Zero || !m.process.WaitForInputIdle(1000) && timelock++ < 10)
                    {
                        Thread.Sleep(1000);
                        m.process.Refresh();
                    }

                    if (timelock >= 10) continue;
                    a.process = m;

                    mf.SetActive(i, true);
                    GWMem.FindAddressesIfNeeded(m);
                    while (m.Read<ushort>(GWMem.CharnamePtr) == 0 && timelock++ < 60)
                    {
                        Thread.Sleep(1000);
                        m.process.Refresh();
                    }
                    if (!string.IsNullOrEmpty(a.character) && m.process.MainWindowTitle == "Guild Wars")
                    {
                        SetWindowText(m.process.MainWindowHandle, a.character);
                    }

                    LoadTextures("C:\\Users\\m\\OneDrive\\Desktop\\programs\\gw1\\Minimalus_Dub.tpf");

                    Thread.Sleep(5000);
                }

                mutex.WaitOne();

                for (var i = 0; i < accounts.Length; ++i)
                {
                    if (!accounts[i].active) continue;
                    if (accounts[i].process.process.HasExited)
                    {
                        mf.SetActive(i, false);
                    }
                }

                mutex.ReleaseMutex();

                Thread.Sleep(1000);
            }
        });
        Application.Run(mf);
    }

}