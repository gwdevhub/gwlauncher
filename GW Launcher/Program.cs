using GW_Launcher.Forms;
using GW_Launcher.uMod;

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
            var text = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<GlobalSettings>(text) ?? new GlobalSettings();
        }
        catch (FileNotFoundException)
        {
            var settings = new GlobalSettings();
            var result = MessageBox.Show(@"Would you like to encrypt the account info?", @"Encryption", MessageBoxButtons.YesNo);
            if (result == DialogResult.No) { settings.Encrypt = false; }
            return settings;
        }

    }
}

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";
    public static AccountManager accounts = new();
    public static Thread mainthread = new(() => {});
    public static Mutex mutex = new();
    public static Mutex? gwlMutex;
    public static GlobalSettings settings = GlobalSettings.Load();

    [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hwnd, string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

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
            // overwrite files
            var filename = Path.Combine(location, "GWML.dll");
            File.WriteAllBytes(filename, Properties.Resources.GWML);
            var filenameumod = Path.Combine(location, "d3d9.dll");
            try
            {
                File.WriteAllBytes(filenameumod, Properties.Resources.d3d9);
            }
            catch (Exception)
            {
                // use the file that already exists
            }
        }

        try
        {
            accounts = new AccountManager("Accounts.json");
        }
        catch (Exception)
        {
            return;
        }
        foreach (var accounts in accounts)
        {
            accounts.active = false;
        }

        using var mainForm = new MainForm();
        mainForm.Location = new Point(-1000, -1000);
        mainForm.FormClosing += (sender, e) => { settings.Save(); };

        mainthread = new Thread(() =>
        {
            var mainClosed = false;
            mainForm.FormClosed += (sender, a) => { mainClosed = true; };
            while (!mainClosed)
            {
                while (mainForm.needtolaunch.Count > 0)
                {
                    var i = mainForm.needtolaunch.Dequeue();
                    var account = accounts[i];
                    if (account.active && account.process != null && account.process.process.MainWindowHandle != IntPtr.Zero)
                    {
                        SetForegroundWindow(account.process.process.MainWindowHandle);
                        continue;
                    }
                    var memory = MulticlientPatch.LaunchClient(account);

                    uint timelock = 0;
                    while (memory.process.MainWindowHandle == IntPtr.Zero || !memory.process.WaitForInputIdle(1000) && timelock++ < 10)
                    {
                        Thread.Sleep(1000);
                        memory.process.Refresh();
                    }

                    if (timelock >= 10) continue;
                    account.process = memory;
                    account.texClient?.Send();

                    mainForm.SetActive(i, true);
                    GWMem.FindAddressesIfNeeded(memory);
                    while (memory.Read<ushort>(GWMem.CharnamePtr) == 0 && timelock++ < 60)
                    {
                        Thread.Sleep(1000);
                        memory.process.Refresh();
                    }
                    if (!string.IsNullOrEmpty(account.character) && memory.process.MainWindowTitle == "Guild Wars")
                    {
                        SetWindowText(memory.process.MainWindowHandle, account.character);
                    }

                    Thread.Sleep(5000);
                }

                mutex.WaitOne();

                for (var i = 0; i < accounts.Length; ++i)
                {
                    if (!accounts[i].active) continue;
                    var gwcaMemory = accounts[i].process;
                    if (gwcaMemory != null && !gwcaMemory.process.HasExited) continue;
                    mainForm.SetActive(i, false);
                    accounts[i].Dispose();
                }

                mutex.ReleaseMutex();

                Thread.Sleep(1000);
            }
        });
        Application.Run(mainForm);
    }

}