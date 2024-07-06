using System.Collections.Concurrent;
using System.Extensions;
using GW_Launcher.Forms;
using GW_Launcher.Guildwars;
using Octokit;
using Account = GW_Launcher.Classes.Account;
using Application = System.Windows.Forms.Application;
using FileMode = System.IO.FileMode;
using ThreadState = System.Threading.ThreadState;

namespace GW_Launcher;

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";
    public static volatile bool shouldClose = false;
    private static volatile bool mainThreadRunning = false;
    public static AccountManager accounts = new();
    public static Thread mainthread = null!;
    public static Mutex mutex = new();
    internal static Mutex? gwlMutex;
    private static MainForm? mainForm;
    private static bool gotMutex = false;
    public static GlobalSettings settings = GlobalSettings.Load();

    private static Queue<int> needtolaunch = new Queue<int>();

    private static string command_arg_launch_account_name = "";

    [DllImport("user32.dll", EntryPoint = "SetWindowText", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hwnd, string lpString);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    static bool IsProcessOpen(string name)
    {
        foreach (Process clsProcess in Process.GetProcesses())
        {
            if (clsProcess.ProcessName.Contains(name))
                return true;
        }
        return false;
    }

    static bool WaitFor(Func<bool> test_func, uint timeout_ms = 10000)
    {
        uint elapsed = 0;
        bool ok = test_func();

        while (!ok)
        {
            if (elapsed > timeout_ms)
                break;
            Thread.Sleep(200);
            elapsed += 200;
            ok = test_func();
        }
        return ok;
    }

    private static string? LaunchAccount(string account_name)
    {
        int found = accounts.IndexOf(account_name);
        if (found == -1)
            return "Failed to find account for " + account_name;
        return LaunchAccount(found);
    }
    static Account? GetAccountByIndex(int account_index)
    {
        return accounts[account_index];
    }
    static string GetAccountName(int account_index)
    {
        var account = GetAccountByIndex(account_index);
        return account == null ? "" : account.Name;
    }

    private static string? LaunchAccount(int account_index)
    {
        var account = accounts[account_index];
        mainForm?.SetAccountState(account_index, "Launching");
        GWCAMemory? memory = null;
        if (!File.Exists(account.gwpath))
            return "Path to the Guild Wars executable incorrect, aborting launch.";
        if (account.process != null)
            memory = account.process;
        if (memory == null)
        {
            if (IsProcessOpen(account.gwpath))
                return "The Guild Wars executable at " + account.gwpath + " is already running";
            var res = MulticlientPatch.LaunchClient(account, out memory);
            if (res != null)
                return res;
        }
        if (memory == null)
            return "Failed to launch account.";

        uint timeout = 10000;
        bool ok = WaitFor(() => {
            memory.process.Refresh();
            return memory.process.MainWindowHandle != IntPtr.Zero;
        }, timeout);
        if (!ok)
        {
            memory.process.Kill();
            return "Failed to wait for MainWindowHandle after " + (timeout / 1000) + " seconds.";
        }
        ok = WaitFor(() => {
            memory.process.Refresh();
            return memory.process.WaitForInputIdle(1000);
        }, timeout);
        if (!ok)
        {
            memory.process.Kill();
            return "Failed to wait for WaitForInputIdle after " + (timeout / 1000) + " seconds.";
        }

        SetForegroundWindow(memory.process.MainWindowHandle);

        account.process = memory;

        GWMemory.FindAddressesIfNeeded(memory);
        ok = WaitFor(() => memory.Read<ushort>(GWMemory.CharnamePtr) != 0 || memory.process.Responding, timeout);
        if (!ok)
        {
            memory.process.Kill();
            return "Failed to wait for CharnamePtr after " + (timeout / 1000) + " seconds.";
        }

        if (memory.process.MainWindowTitle == "Guild Wars")
        {
            SetWindowText(memory.process.MainWindowHandle, account.Name);
        }
        return null;
    }
    [STAThread]
    internal static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (!ParseCommandLineArgs())
        {
            Exit();
            return; // Error message already displayed
        }
        if (!LoadAccountsJson())
        {
            Exit();
            return; // Error message already displayed
        }

        if (settings.CheckForUpdates)
        {
            Task.Run(CheckGitHubNewerVersion);
            Task.Run(CheckGitHubGModVersion);
            Task.Run(CheckForGwExeUpdates);
        }
        settings.Save();

        if (command_arg_launch_account_name.Length > 0)
        {
            var res = LaunchAccount(command_arg_launch_account_name);
            if (res != null)
            {
                MessageBox.Show(@"Failed to launch account " + command_arg_launch_account_name + "\n" + res);
            }
            Exit();
            return;
        }

        // Only try to create and grab the mutex if we're in the main program
        if (!InitialiseGWLauncherMutex())
        {
            Exit();
            return; // Error message already displayed
        }

        mainThreadRunning = true;
        mainthread = new Thread(() =>
        {

            while (!shouldClose)
            {
                UnlockMutex();
                if (needtolaunch.Any())
                {
                    if (!LockMutex()) break;
                    var accountName = GetAccountName(needtolaunch.Dequeue());

                    var res = LaunchAccount(accountName);
                    UnlockMutex();
                    if (res != null)
                    {
                        MessageBox.Show(@"Failed to launch account " + accountName + "\n" + res);
                    }
                }

                if (!LockMutex()) continue;

                for (var i = 0; mainForm != null && i < accounts.Length; i++)
                {
                    var state = "Inactive";
                    var gwcaMemory = accounts[i].process;
                    if (gwcaMemory != null && gwcaMemory.process != null && !gwcaMemory.process.HasExited)
                    {
                        state = "Active";
                    }
                    if (state != "Active" && accounts[i].process != null)
                    {
                        accounts[i].process = null;
                    }
                    if (accounts[i].state != state)
                    {
                        mainForm?.SetAccountState(i, state);
                    }
                }

                UnlockMutex();

                Thread.Sleep(1000);
            }
            mainThreadRunning = false;
        });

        // Main application
        mainForm = new MainForm(settings.LaunchMinimized);
        mainForm.FormClosed += (_, _) => { Exit(); };
        Application.Run(mainForm);
    }
    public static bool QueueLaunch(int index)
    {
        if (index < 0 || accounts.Length <= index)
            return false;
        needtolaunch.Enqueue(index);
        return true;
    }

    public static void Exit()
    {
        while (needtolaunch.Count > 0)
            Thread.Sleep(16);
        shouldClose = true;
        while (mainThreadRunning)
            Thread.Sleep(16);
        if (gwlMutex != null)
        {
            gwlMutex.Close();
            gwlMutex = null;
        }
    }
    private static bool ParseCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-launch":
                    i++;
                    if (i >= args.Length)
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
            if (settings.Encrypt) return false; // error message already shown
            MessageBox.Show("""
                            Couldn't load account information, there might be an error in the .json.
                            GW Launcher will close.
                            """);

            return false;
        }
    }

    private static bool LockMutex()
    {
        gotMutex = gotMutex || mutex.WaitOne(1000);
        return gotMutex;
    }
    private static void UnlockMutex()
    {
        if (gotMutex)
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
        var client = new GitHubClient(new ProductHeaderValue("GWLauncher"));
        var releases = await client.Repository.Release.GetAll("gwdevhub", "gwlauncher");

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
        if (mainthread.ThreadState == ThreadState.Running &&
            !mainthread.Join(5000))
        {
            return;
        }

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

    private static async Task CheckGitHubGModVersion()
    {
        var location = Path.GetDirectoryName(AppContext.BaseDirectory);
        var gmod = Path.Combine(location!, "gMod.dll");
        //Get all releases from GitHub
        var client = new GitHubClient(new ProductHeaderValue("gMod"));
        var releases = await client.Repository.Release.GetAll("gwdevhub", "gMod");

        if (!releases.Any(r => !r.Prerelease && !r.Draft))
        {
            return;
        }

        var release = releases.First(r => !r.Prerelease && !r.Draft);
        var tagName = Regex.Replace(release.TagName, @"[^\d\.]", "");
        var latestVersion = new Version(tagName);
        var minVersion = new Version("1.5.2");
        if (latestVersion.CompareTo(minVersion) <= 0)
        {
            return;
        }

        string strVersion;
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(gmod);
            strVersion = fvi.FileVersion!;
        }
        catch (FileNotFoundException)
        {
            strVersion = "1.0.0";
        }
        var localVersion = new Version(strVersion);

        var versionComparison = localVersion.CompareTo(latestVersion);
        if (versionComparison >= 0)
        {
            return;
        }

        var latest = releases[0];

        var asset = latest.Assets.First(a => a.Name == "gMod.dll");
        if (asset == null)
        {
            return;
        }

        var uri = new Uri(asset.BrowserDownloadUrl);
        var httpClient = new HttpClient();
        await using var s = await httpClient.GetStreamAsync(uri);
        await using var fs = new FileStream(gmod, FileMode.Create);
        await s.CopyToAsync(fs);
    }

    private static async Task CheckForGwExeUpdates()
    {
        try
        {
            var latestFileId = await GwDownloader.GetLatestGwExeFileIdAsync();
            if (latestFileId == 0) return;
            List<Account> accsToUpdate = new List<Account>();
            List<Account> accsChecked = new List<Account>();

            foreach (var account in accounts)
            {
                if (accsChecked.Select(a => a.gwpath).Contains(account.gwpath)) continue;
                if (!File.Exists(account.gwpath)) continue;
                var currentFileId = FileIdFinder.GetFileId(account.gwpath);

                accsChecked.Add(account);
                if (currentFileId == latestFileId)
                {
                    continue;
                }

                accsToUpdate.Add(account);
            }

            if (accsToUpdate.Count == 0) return;
            var accNames = string.Join(',', accsToUpdate.Select(acc => acc.Name));
            var ok = MessageBox.Show($"Accounts {accNames} are out of date. Update now?", "GW Update", MessageBoxButtons.YesNo);
            if (ok == DialogResult.Yes)
            {
                AdminAccess.RestartAsAdminPrompt(true);
                if (mainForm is not null)
                {
                    await mainForm.Invoke(async () =>
                    {
                        try
                        {
                            mutex.WaitOne();
                            await mainForm.UpdateAccountsGui(accsToUpdate);
                        }
                        finally
                        {
                            mutex.ReleaseMutex();
                        }
                    });
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception
            Console.WriteLine($"Error checking for Gw.exe updates: {ex.Message}");
            MessageBox.Show($"Error checking for updates: {ex.Message}", "Update Check Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
