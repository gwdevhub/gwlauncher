using GW_Launcher.Forms;
namespace GW_Launcher;

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";
    public static volatile bool shouldClose = false;
    public static volatile bool mainThreadRunning = false;
    public static AccountManager accounts = new();
    public static Thread mainthread = null!;
    public static Mutex mutex = new();
    public static Mutex? gwlMutex;
    private static MainForm? mainForm;
    private static bool gotMutex = false;
    public static GlobalSettings settings = GlobalSettings.Load();

    private static Queue<int> needtolaunch = new Queue<int>();

    private static string command_arg_launch_account_name = "Demia Frelluis";

    [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hwnd, string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [STAThread]
    internal static void Main()
    {
        if (!ParseCommandLineArgs())
        {
            Cleanup();
            return; // Error message already displayed
        }
        if(!LoadAccountsJson())
        {
            Cleanup();
            return; // Error message already displayed
        }

        if (settings.CheckForUpdates)
        {
            Task.Run(CheckGitHubNewerVersion);
        }

        CreateUmodD3d9Dll();

        settings.Save();
        mainThreadRunning = true;
        mainthread = new Thread(() =>
        {
            
            while (!shouldClose)
            {
                UnlockMutex();
                while (needtolaunch.Any())
                {
                    UnlockMutex();
                    if (!LockMutex()) break;
                    var i = needtolaunch.Dequeue();
                    var account = accounts[i];
                    if (!File.Exists(account.gwpath))
                    {
                        MessageBox.Show(@"Path to the Guild Wars executable incorrect, aborting launch.");
                        continue;
                    }
                    switch (account.active)
                    {
                        case true when account.process != null && account.process.process.MainWindowHandle != IntPtr.Zero:
                            SetForegroundWindow(account.process.process.MainWindowHandle);
                            continue;
                        case true:
                            continue;
                    }

                    var memory = MulticlientPatch.LaunchClient(account);
                    if (memory == null)
                    {
                        MessageBox.Show(@"Failed to launch account.");
                        continue;
                    }

                    uint timelock = 0;
                    while (timelock++ < 10 && (memory.process.MainWindowHandle == IntPtr.Zero ||
                                               !memory.process.WaitForInputIdle(1000)))
                    {
                        Thread.Sleep(1000);
                        memory.process.Refresh();
                    }

                    if (timelock >= 10)
                    {
                        continue;
                    }

                    account.process = memory;

                    if(mainForm != null)
                        mainForm.SetActive(i, true);
                    GWMemory.FindAddressesIfNeeded(memory);
                    while (memory.Read<ushort>(GWMemory.CharnamePtr) == 0 && timelock++ < 60)
                    {
                        Thread.Sleep(1000);
                        memory.process.Refresh();
                    }

                    if (memory.process.MainWindowTitle == "Guild Wars")
                    {
                        SetWindowText(memory.process.MainWindowHandle, account.Name);
                    }
                    

                    Thread.Sleep(1000);
                }
                if (!LockMutex()) continue;

                for (var i = 0; i < accounts.Length; i++)
                {
                    if (!accounts[i].active)
                    {
                        continue;
                    }

                    var gwcaMemory = accounts[i].process;
                    if (gwcaMemory != null && !gwcaMemory.process.HasExited)
                    {
                        continue;
                    }

                    accounts[i].process = null;
                    if(mainForm != null)
                        mainForm.SetActive(i, false);
                }

                UnlockMutex();

                Thread.Sleep(1000);
            }
            mainThreadRunning = false;
        });

        mainthread.Start();


        if (command_arg_launch_account_name.Length > 0)
        {
            if(!LaunchAccount(command_arg_launch_account_name))
            {
                MessageBox.Show(@"Failed to launch account " + command_arg_launch_account_name);
            }
            Cleanup();
            return;
        }
        // Only try to create and grab the mutex if we're in the main program
        if (!InitialiseGWLauncherMutex())
        {
            Cleanup();
            return; // Error message already displayed
        }

        // Main application
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        mainForm = new MainForm();
        mainForm.FormClosed += (_, _) => { shouldClose = true; };
        Application.Run(mainForm);
    }
    public static bool LaunchAccount(int index)
    {
        if (index < 0 || accounts.Length <= index)
            return false;
        needtolaunch.Enqueue(index);
        return true;
    }
    public static bool LaunchAccount(string name)
    {
        return LaunchAccount(accounts.FindByName(name));
    }
    private static void Cleanup()
    {
        while (needtolaunch.Count > 0)
            Thread.Sleep(16);
        shouldClose = true;
        while (mainThreadRunning)
            Thread.Sleep(16);
        if(gwlMutex != null)
        {
            gwlMutex.Close();
            gwlMutex = null;
        }
    }
    private static bool ParseCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        for(var i=1;i<args.Length;i++)
        {
            switch(args[i])
            {
                case "-launch":
                    i++;
                    if(i >= args.Length)
                    {
                        MessageBox.Show(@"No value for command line argument -launch");
                        return false;
                    }
                    command_arg_launch_account_name = args[i];
                    break;
            }
        }
        return true;
    }
    private static bool InitialiseGWLauncherMutex()
    {
        // Check to see if another instance is running
        if (Mutex.TryOpenExisting(GwlMutexName, out gwlMutex))
        {
            //MessageBox.Show(@"GW Launcher already running. GW Launcher will close.");
            return false;
        }
        gwlMutex = new Mutex(true, GwlMutexName);
        return true;
    }
    private static bool LoadAccountsJson()
    {
        // Load accounts
        try
        {
            accounts = new AccountManager("Accounts.json");
            return true;
        }
        catch (Exception)
        {
            MessageBox.Show(@"Couldn't load account information, there might be an error in the .json.
GW Launcher will close.");
            
            return false;
        }
    }
    private static void CreateUmodD3d9Dll()
    {
        var location = Path.GetDirectoryName(AppContext.BaseDirectory);
        if (location != null)
        {
            // dump correct version of umod d3d9.dll
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
    }

    private static bool LockMutex()
    {
        gotMutex = gotMutex || mutex.WaitOne(1000);
        return gotMutex;
    }
    private static void UnlockMutex()
    {
        if(gotMutex)
        {
            mutex.ReleaseMutex();
            gotMutex = false;
        }
    }
    private static async Task CheckGitHubNewerVersion()
    {
        const string oldName = ".old.exe";
        const string newName = ".new.exe";
        if (settings.AutoUpdate && (File.Exists(oldName) || File.Exists(newName)))
        {
            File.Delete(oldName);
            File.Delete(newName);
        }

        //Get all releases from GitHub
        var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("GWLauncher"));
        var releases = await client.Repository.Release.GetAll("GregLando113", "gwlauncher");

        if (!releases.Any(r => !r.Prerelease && !r.Draft))
        {
            return;
        }

        var release = releases.First(r => !r.Prerelease && !r.Draft);
        var tagName = Regex.Replace(release.TagName, @"[^\d\.]", "");
        var latestVersion = new Version(tagName);
        var minVersion = new Version("13.0");
        if (latestVersion.CompareTo(minVersion) <= 0)
        {
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var fvi = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? "");
        var version = assembly.GetName().Version?.ToString();
        if (version == null && fvi.FileVersion == null)
        {
            return;
        }

        var strVersion = fvi.FileVersion ?? version ?? "";
        var localVersion = new Version(strVersion);

        var versionComparison = localVersion.CompareTo(latestVersion);
        if (versionComparison >= 0)
        {
            return;
        }

        var latest = releases[0];

        if (!settings.AutoUpdate)
        {
            var msgBoxResult = MessageBox.Show(
                $@"New version {tagName} available online. Visit page?",
                @"GW Launcher",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (msgBoxResult == DialogResult.Yes)
            {
                Process.Start("explorer.exe", latest.HtmlUrl);
            }

            return;
        }

        var currentName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName);
        if (currentName == null)
        {
            return;
        }

        var asset = latest.Assets.First(a => a.Name == "GW_Launcher.exe");
        if (asset == null)
        {
            return;
        }

        var uri = new Uri(asset.BrowserDownloadUrl);
        var httpClient = new HttpClient();
        await using (var s = await httpClient.GetStreamAsync(uri))
        {
            await using var fs = new FileStream(newName, FileMode.Create);
            await s.CopyToAsync(fs);
        }

        mutex.WaitOne();
        shouldClose = true;
        if (mainthread.ThreadState == System.Threading.ThreadState.Running &&
            !mainthread.Join(5000))
            return;
        mutex.Close();
        gwlMutex?.Close();

        File.Move(currentName, oldName);

        File.Move(newName, currentName);

        var fileName = Environment.ProcessPath;
        var processInfo = new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = fileName,
            Arguments = "restart"
        };

        Application.Restart();
        Process.Start(processInfo);
        Environment.Exit(0);
    }
}
