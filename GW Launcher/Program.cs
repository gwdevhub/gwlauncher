using GW_Launcher.Forms;

namespace GW_Launcher;

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";
    public static bool shouldClose;
    public static AccountManager accounts = new();
    public static Thread mainthread = null!;
    public static Mutex mutex = new();
    public static Mutex? gwlMutex;
    private static bool _gotMutex;
    public static GlobalSettings settings = GlobalSettings.Load();

    [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hwnd, string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

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

        if (settings.CheckForUpdates)
        {
            Task.Run(CheckGitHubNewerVersion);
        }

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

        try
        {
            accounts = new AccountManager("Accounts.json");
        }
        catch (Exception e)
        {
            MessageBox.Show(@"Couldn't load account information, there might be an error in the .json.
GW Launcher will close.
" + e.Message);
            gwlMutex.Close();
            return;
        }

        try
        {
            settings.Save();
        }
        catch (Exception e)
        {
            MessageBox.Show(@"Couldn't save settings to Settings.json.
GW Launcher will close.
" + e.Message);
            gwlMutex.Close();
            return;
        }

        var isPipeRunning = Directory.GetFiles(@"\\.\pipe\", @"Game2uMod").Any();
        if (isPipeRunning && accounts.Any(a => ModManager.GetTexmods(a.gwpath, a.mods).Any()))
        {
            MessageBox.Show(@"uMod may be running in the background. Textures may not load.");
        }

        using var mainForm = new MainForm();
        mainForm.FormClosed += (_, _) => { shouldClose = true; };

        mainthread = new Thread(() =>
        {
            while (!shouldClose)
            {
                UnlockMutex();
                while (mainForm.needtolaunch.Any())
                {
                    UnlockMutex();
                    if (!LockMutex()) break;
                    var i = mainForm.needtolaunch.Dequeue();
                    var account = accounts[i];
                    if (!File.Exists(account.gwpath))
                    {
                        MessageBox.Show(@"Path to the Guild Wars executable incorrect, aborting launch.");
                        continue;
                    }
                    switch (account.active)
                    {
                        case true when account.process != null &&
                                       account.process.process.MainWindowHandle != IntPtr.Zero:
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

                    var timelock = 0;
                    while (timelock++ < 5 && (memory.process.MainWindowHandle == IntPtr.Zero ||
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

                    mainForm.SetActive(i, true);
                    GWMemory.FindAddressesIfNeeded(memory);
                    while (memory.Read<ushort>(GWMemory.CharnamePtr) == 0 && timelock++ < 5)
                    {
                        Thread.Sleep(1000);
                        memory.process.Refresh();
                    }

                    if (memory.process.MainWindowTitle == "Guild Wars")
                    {
                        SetWindowText(memory.process.MainWindowHandle, account.Name);
                    }

                    UnlockMutex();
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
                    if (gwcaMemory is {process.HasExited: false})
                    {
                        continue;
                    }

                    accounts[i].process = null;
                    mainForm.SetActive(i, false);
                }
                UnlockMutex();

                Thread.Sleep(1000);
            }
            Application.Exit();
        });
        Application.Run(mainForm);
    }

    private static bool LockMutex()
    {
        if (_gotMutex) return true;
        _gotMutex = mutex.WaitOne(1000);
        return _gotMutex;
    }
    private static void UnlockMutex()
    {
        if (!_gotMutex) return;
        mutex.ReleaseMutex();
        _gotMutex = false;
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
