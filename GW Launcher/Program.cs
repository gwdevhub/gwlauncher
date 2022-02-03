using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace GW_Launcher
{
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
                return JsonConvert.DeserializeObject<GlobalSettings>(txt);
            }
            catch (FileNotFoundException)
            {
                return new GlobalSettings();
            }

        }
    }

    internal static class Program
    {
        private const string GwlMutexName = "gwl_instance_mutex";
        public static AccountManager accounts;
        public static Thread mainthread;
        public static Mutex mutex = new Mutex();
        public static Mutex gwlMutex;
        public static GlobalSettings settings;

        [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hwnd, String lpString);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [STAThread]
        internal static void Main()
        {
            var location = Path.GetDirectoryName(AppContext.BaseDirectory);
            if (location != null)
            {
                var filename = Path.Combine(location, "GWML.dll");
                if (!File.Exists(filename))
                {
                    File.WriteAllBytes(filename, Properties.Resources.GWML);
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Mutex.TryOpenExisting(GwlMutexName, out gwlMutex))
            {
                return;
            }

            gwlMutex = new Mutex(true, GwlMutexName);

            settings = GlobalSettings.Load();

            accounts = new AccountManager("Accounts.json");
            foreach (var t in accounts)
            {
                t.active = false;
            }

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
                        var m = GWMC_CS.MulticlientPatch.LaunchClient(a.gwpath,
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
}
