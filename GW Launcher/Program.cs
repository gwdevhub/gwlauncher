using GW_Launcher.Forms;
using GW_Launcher.Guildwars;
using Account = GW_Launcher.Classes.Account;
using Application = System.Windows.Forms.Application;
using File = System.IO.File;
using FileMode = System.IO.FileMode;
using ThreadState = System.Threading.ThreadState;

namespace GW_Launcher;

internal static class Program
{
    private const string GwlMutexName = "gwl_instance_mutex";
    public static volatile bool ShouldClose = false;
    private static volatile bool _mainThreadRunning = false;
    public static AccountManager Accounts = new();
    public static Thread Mainthread = null!;
    public static Mutex Mutex = new();
    internal static Mutex? GwlMutex;
    private static MainForm? _mainForm;
    private static bool _gotMutex = false;
    public static GlobalSettings Settings = GlobalSettings.Load();

    private static Queue<(int index, bool ctrlHeld)> _needtolaunch = new();

    private static string _commandArgLaunchAccountName = "";

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);


    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);


    [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName([In] IntPtr hProcess, [In] uint dwFlags, [Out] StringBuilder lpExeName, [In, Out] ref uint lpdwSize);

    private static string? GetProcessPath(Process process)
    {

        var fileNameBuilder = new StringBuilder(1024);
        var bufferLength = (uint)fileNameBuilder.Capacity + 1;
        return QueryFullProcessImageName(process.Handle, 0, fileNameBuilder, ref bufferLength) ?
            fileNameBuilder.ToString() :
            null;

    }

    private static bool IsProcessOpen(string name)
    {
        var basename = Path.GetFileNameWithoutExtension(name);
        var processes = Process.GetProcesses();
        foreach (var clsProcess in processes)
        {
            if (!clsProcess.ProcessName.Equals(basename))
                continue;
            var path = GetProcessPath(clsProcess);
            if (path != null && path.Contains(name))
                return true;
        }
        return false;
    }

    private static bool WaitFor(Func<bool> testFunc, uint timeoutMs = 10000)
    {
        uint elapsed = 0;
        var ok = testFunc();

        while (!ok)
        {
            if (elapsed > timeoutMs)
                break;
            Thread.Sleep(200);
            elapsed += 200;
            ok = testFunc();
        }

        return ok;
    }

    private static string? CreateSteamAppIdFile(Account account)
    {
        var gwDirectory = Path.GetDirectoryName(account.gwpath);
        if (string.IsNullOrEmpty(gwDirectory))
            return "Failed to determine Guild Wars directory path.";
        var steamAppIdPath = Path.Combine(gwDirectory, "steam_appid.txt");
        if (account.is_steam_account)
        {
            // Create steam_appid.txt with value 29720 in the same folder as the gwpath
            try
            {
                File.WriteAllText(steamAppIdPath, "29720");
            }
            catch (Exception ex)
            {
                return $"Failed to create steam_appid.txt: {ex.Message}";
            }
        }
        return null;
    }
    private static string? DeleteSteamAppIdFile(Account account)
    {
        var gwDirectory = Path.GetDirectoryName(account.gwpath);
        if (string.IsNullOrEmpty(gwDirectory))
            return "Failed to determine Guild Wars directory path.";
        var steamAppIdPath = Path.Combine(gwDirectory, "steam_appid.txt");
        if (File.Exists(steamAppIdPath))
        {
            File.Delete(steamAppIdPath);
        }
        return null;
    }

    private static string? LaunchAccount(string accountName, bool ctrlHeld = false)
    {
        var found = Accounts.IndexOf(accountName);
        if (found == -1)
            return "Failed to find account for " + accountName;
        var result = DeleteSteamAppIdFile(Accounts[found]);
        if (result != null) return result;
        result = CreateSteamAppIdFile(Accounts[found]);
        if (result != null) return result;
        result = LaunchAccount(Accounts[found], ctrlHeld);
        DeleteSteamAppIdFile(Accounts[found]);
        return result;
    }

    static Account? GetAccountByIndex(int accountIndex)
    {
        return Accounts[accountIndex];
    }

    static string GetAccountName(int accountIndex)
    {
        var account = GetAccountByIndex(accountIndex);
        return account == null ? "" : account.Name;
    }

    private static string? LaunchAccount(Account account, bool ctrlHeld = false)
    {
        _mainForm?.SetAccountState(Accounts.IndexOf(account), "Launching");
        GWCAMemory? memory = null;
        if (!File.Exists(account.gwpath))
            return "Path to the Guild Wars executable incorrect, aborting launch.";
        if (account.process != null)
            memory = account.process;
        var gwDirectory = Path.GetDirectoryName(account.gwpath);
        if (string.IsNullOrEmpty(gwDirectory))
            return "Failed to determine Guild Wars directory path.";
        var steamAppIdPath = Path.Combine(gwDirectory, "steam_appid.txt");
        if (account.is_steam_account)
        {
            var steamProcesses = Process.GetProcessesByName("steam");
            if (steamProcesses.Length == 0)
            {
                return "Steam is not running. Please start Steam before launching a Steam account.";
            }
        }
        if (memory == null)
        {
            try
            {
                if (IsProcessOpen(account.gwpath))
                    return "The Guild Wars executable at " + account.gwpath + " is already running";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            var res = MulticlientPatch.LaunchClient(account, ctrlHeld, out memory);
            if (res != null)
                return res;
        }

        if (memory == null)
            return "Failed to launch account.";

        uint timeout = 10000;
        var ok = WaitFor(() =>
        {
            memory.process.Refresh();
            return memory.process.MainWindowHandle != IntPtr.Zero;
        }, timeout);
        if (!ok)
        {
            memory.process.Kill();
            return "Failed to wait for MainWindowHandle after " + (timeout / 1000) + " seconds.";
        }

        ok = WaitFor(() =>
        {
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
        ok = WaitFor(() => memory.Read<ushort>(GWMemory.CharnamePtr) != 0 && memory.process.Responding, timeout);
        if (!ok)
        {
            // memory.process.Kill();
            Console.WriteLine("Failed to wait for CharnamePtr after " + (timeout / 1000) + " seconds.");
        }
        timeout = 5000;
        ok = WaitFor(() =>
        {
            memory.process.Refresh();
            return memory.process.MainWindowTitle != "";
        }, timeout);
        if (ok && memory.process.MainWindowTitle == "Guild Wars" || memory.process.MainWindowTitle == "Guild Wars Reforged")
        {
            // NB: Window may not be ready for title change, or GW may be (re)setting window title as part of render process.
            ok = WaitFor(() =>
            {
                memory.process.Refresh();
                var chars = Marshal.StringToHGlobalAnsi(account.Name);
                SendMessage(memory.process.MainWindowHandle, 0xc, 0, chars);
                memory.process.Refresh();
                return memory.process.MainWindowTitle != "Guild Wars" && memory.process.MainWindowTitle != "Guild Wars Reforged";
            }, timeout);
        }

        if (!memory.process.Responding)
        {
            memory.process.Kill();
            return "Failed to wait for process to respond after " + (timeout / 1000) + " seconds.";
        }

        return null;
    }

    private static string? LaunchAccount(int accountIndex)
    {
        return LaunchAccount(Accounts[accountIndex]);
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

        WarnIfInGwDirectory();

        if (Settings.CheckForUpdates)
        {
            Task.Run(CheckGitHubNewerVersion);
            Task.Run(CheckGitHubGModVersion);
            Task.Run(async () => await CheckForGwExeUpdates(false, false));
        }

        Settings.Save();

        var hasMutex = InitialiseGwLauncherMutex();

        if (_commandArgLaunchAccountName.Length > 0 && LoadAccountsJson())
        {
            var res = LaunchAccount(_commandArgLaunchAccountName);
            if (res != null)
            {
                MessageBox.Show(@"Failed to launch account " + _commandArgLaunchAccountName + "\n" + res);
            }

            Exit();
            return; // Error message already displayed
        }

        // Only try to create and grab the mutex if we're in the main program
        if (!hasMutex)
        {
            Exit();
            return; // Error message already displayed
        }

        if (!LoadAccountsJson())
        {
            Exit();
            return; // Error message already displayed
        }

        _mainThreadRunning = true;
        Mainthread = new Thread(() =>
        {
            while (!ShouldClose)
            {
                UnlockMutex();
                if (_needtolaunch.Any())
                {
                    if (!LockMutex()) break;
                    var (queuedIndex, queuedCtrlHeld) = _needtolaunch.Dequeue();
                    var accountName = GetAccountName(queuedIndex);

                    var res = LaunchAccount(accountName, queuedCtrlHeld);
                    UnlockMutex();
                    if (res != null)
                    {
                        MessageBox.Show(@"Failed to launch account " + accountName + "\n" + res);
                    }
                }

                if (!LockMutex()) continue;

                for (var i = 0; _mainForm != null && i < Accounts.Length; i++)
                {
                    var state = "Inactive";
                    var gwcaMemory = Accounts[i].process;
                    if (gwcaMemory != null && gwcaMemory.process != null && !gwcaMemory.process.HasExited)
                    {
                        state = "Active";
                    }

                    if (state != "Active" && Accounts[i].process != null)
                    {
                        Accounts[i].process = null;
                    }

                    if (Accounts[i].state != state)
                    {
                        _mainForm?.SetAccountState(i, state);
                    }
                }

                UnlockMutex();

                Thread.Sleep(1000);
            }

            _mainThreadRunning = false;
        });

        // Main application
        _mainForm = new MainForm(Settings.LaunchMinimized);
        _mainForm.FormClosed += (_, _) => { Exit(); };
        Application.Run(_mainForm);
    }

    public static bool QueueLaunch(int index, bool ctrlHeld = false)
    {
        if (index < 0 || Accounts.Length <= index)
            return false;
        _needtolaunch.Enqueue((index, ctrlHeld));
        return true;
    }

    public static void Exit()
    {
        while (_needtolaunch.Count > 0)
            Thread.Sleep(16);
        ShouldClose = true;
        while (_mainThreadRunning)
            Thread.Sleep(16);
        if (GwlMutex != null)
        {
            GwlMutex.Close();
            GwlMutex = null;
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

                    _commandArgLaunchAccountName = args[i];
                    break;
            }
        }

        return true;
    }

    private static bool InitialiseGwLauncherMutex()
    {
        // Check to see if another instance is running
        if (Mutex.TryOpenExisting(GwlMutexName, out GwlMutex))
        {
            //MessageBox.Show(@"GW Launcher already running. GW Launcher will close.");
            return false;
        }

        GwlMutex = new Mutex(true, GwlMutexName);
        return true;
    }

    private static bool LoadAccountsJson()
    {
        // Load accounts
        try
        {
            Accounts = new AccountManager("Accounts.json");
            return true;
        }
        catch (OperationCanceledException)
        {
            // Wrong/cancelled master password - the prompt already told the user.
            return false;
        }
        catch (Exception)
        {
            MessageBox.Show("""
                            Couldn't load account information, there might be an error in the .json.
                            GW Launcher will close.
                            """);

            return false;
        }
    }

    private static bool LockMutex()
    {
        _gotMutex = _gotMutex || Mutex.WaitOne(1000);
        return _gotMutex;
    }

    private static void UnlockMutex()
    {
        if (_gotMutex)
        {
            Mutex.ReleaseMutex();
            _gotMutex = false;
        }
    }

    private static void WarnIfInGwDirectory()
    {
        var exePath = Application.ExecutablePath;
        var exeDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        var exeLower = exePath.ToLowerInvariant();

        var inProgramFiles = exeLower.Contains(@"program files\") || exeLower.Contains(@"program files (x86)\");
        var hasGwExe = File.Exists(Path.Combine(exeDirectory, "gw.exe"));

        if (inProgramFiles || hasGwExe)
        {
            MessageBox.Show(
                "This application should not be run from Program Files or the Guild Wars directory.\n\nPlease move GW Launcher to its own folder in your User folder.",
                "Warning",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
        }
    }

    private static async Task CheckGitHubNewerVersion()
    {
        var currentPath = Environment.ProcessPath;
        var exeDir = Path.GetDirectoryName(currentPath);
        if (currentPath == null || exeDir == null)
        {
            return;
        }

        // Anchor the staging files to the exe's own folder rather than the process working
        // directory, which is e.g. System32 when the launcher is started from the Startup
        // folder — there the relative-path swap silently failed and the new exe never ran.
        var oldPath = Path.Combine(exeDir, ".old.exe");
        var newPath = Path.Combine(exeDir, ".new.exe");
        if (File.Exists(oldPath) || File.Exists(newPath))
        {
            try { File.Delete(oldPath); } catch { /* may be locked; best effort */ }
            try { File.Delete(newPath); } catch { /* best effort */ }
        }

        var releases = await GitHubAssets.GetReleasesAsync("gwdevhub", "gwlauncher");

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

        var runtimeInstalled = IsDotNet10DesktopInstalled();
        var assetName = runtimeInstalled
            ? "GW_Launcher_Framework_Dependent.exe"
            : "GW_Launcher.exe";

        var asset = release.Assets.FirstOrDefault(a => a.Name == assetName);
        if (asset == null)
        {
            return;
        }

        // Already running the published build for this release.
        var localPath = Environment.ProcessPath;
        if (localPath != null && GitHubAssets.IsUpToDate(localPath, asset.Sha256))
        {
            return;
        }

        if (!Settings.AutoUpdate)
        {
            var msgBoxResult = MessageBox.Show(
                $@"New version {tagName} available. Download and install it now?",
                @"GW Launcher",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);
            if (msgBoxResult != DialogResult.Yes)
            {
                return;
            }
        }

        var uri = new Uri(asset.DownloadUrl);
        if (Settings.AutoUpdate)
        {
            var httpClient = new HttpClient();
            await using var s = await httpClient.GetStreamAsync(uri);
            await using var fs = new FileStream(newPath, FileMode.Create);
            await s.CopyToAsync(fs);
        }
        else if (!await DownloadFileWithProgress(uri, newPath, $"Updating GW Launcher to {tagName}"))
        {
            return;
        }

        Mutex.WaitOne();
        ShouldClose = true;
        if (Mainthread.ThreadState == ThreadState.Running && !Mainthread.Join(5000))
        {
            return;
        }

        Mutex.Close();
        GwlMutex?.Close();

        // Swap the freshly downloaded exe in for the running one. On failure roll back so the
        // exe at currentPath is always a working launcher, then relaunch whatever is there.
        try
        {
            File.Move(currentPath, oldPath);
            try
            {
                File.Move(newPath, currentPath);
            }
            catch
            {
                File.Move(oldPath, currentPath);
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update failed to install the new exe; keeping the current version: {ex.Message}");
        }

        Relaunch(currentPath, exeDir);
        Environment.Exit(0);
    }

    private static void Relaunch(string exePath, string workingDir)
    {
        // This runs on a thread-pool thread that immediately calls Environment.Exit. Prefer a
        // direct CreateProcess (UseShellExecute = false): it returns only once the child is
        // actually running, so the relaunch can't lose a race with our teardown. ShellExecute
        // hands off to the shell and could return before — or be torn down with — this process,
        // which is what intermittently left the updated instance unstarted. Fall back to a
        // shell launch only if the direct start throws.
        ProcessStartInfo Info(bool useShellExecute) => new()
        {
            FileName = exePath,
            Arguments = "restart",
            WorkingDirectory = workingDir,
            UseShellExecute = useShellExecute
        };

        try
        {
            if (Process.Start(Info(false)) != null)
                return;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update direct relaunch failed, trying shell launch: {ex.Message}");
        }

        try
        {
            Process.Start(Info(true));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Self-update relaunch failed: {ex.Message}");
        }
    }

    private static async Task<bool> DownloadFileWithProgress(Uri uri, string destPath, string title)
    {
        using var cts = new CancellationTokenSource();
        using var dialog = new Form
        {
            Text = title,
            Size = new System.Drawing.Size(360, 130),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var label = new System.Windows.Forms.Label
        {
            Text = "Downloading update...",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        };
        var progressBar = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Minimum = 0,
            Maximum = 100,
            Style = ProgressBarStyle.Marquee
        };
        var cancelBtn = new Button
        {
            Text = "Cancel",
            Dock = DockStyle.Bottom,
            DialogResult = DialogResult.Cancel
        };
        cancelBtn.Click += (_, _) => cts.Cancel();
        dialog.Controls.Add(label);
        dialog.Controls.Add(progressBar);
        dialog.Controls.Add(cancelBtn);

        var success = false;
        var completed = false;

        dialog.FormClosing += (_, _) =>
        {
            if (!completed && !cts.IsCancellationRequested)
                cts.Cancel();
        };

        Task? downloadTask = null;
        dialog.Shown += (_, _) => downloadTask = Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient();
                using var response =
                    await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength;
                if (total is > 0)
                {
                    try { progressBar.Invoke(() => progressBar.Style = ProgressBarStyle.Continuous); }
                    catch { /* dialog gone */ }
                }

                await using var src = await response.Content.ReadAsStreamAsync(cts.Token);
                await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, cts.Token)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                    readTotal += read;
                    if (total is > 0)
                    {
                        var pct = (int)Math.Min(100, readTotal * 100 / total.Value);
                        try { progressBar.Invoke(() => progressBar.Value = pct); }
                        catch { /* dialog gone */ }
                    }
                }

                success = true;
            }
            catch (OperationCanceledException)
            {
                // User cancelled — leave success false.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading update: {ex.Message}");
            }
            finally
            {
                completed = true;
                try { dialog.Invoke(() => dialog.Close()); }
                catch { /* already disposed */ }
            }
        }, cts.Token);

        dialog.ShowDialog();
        if (downloadTask != null)
            await downloadTask;

        if (!success)
        {
            try
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);
            }
            catch
            {
                // best effort cleanup
            }
        }

        return success;
    }

    private static bool IsDotNet10DesktopInstalled()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-runtimes",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            using var reader = process!.StandardOutput;
            var output = reader.ReadToEnd();
            process.WaitForExit();

            return output.Contains("Microsoft.WindowsDesktop.App 10");
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task CheckGitHubGModVersion()
    {
        var location = Path.GetDirectoryName(AppContext.BaseDirectory);
        var gmod = Path.Combine(location!, "gMod.dll");
        var releases = await GitHubAssets.GetReleasesAsync("gwdevhub", "gMod");

        GitHubAsset? asset = null;
        foreach (var release in releases)
        {
            if (release.Prerelease || release.Draft)
            {
                continue;
            }

            var candidateAsset = release.Assets.FirstOrDefault(a => a.Name == "gMod.dll");
            if (candidateAsset == null)
            {
                continue;
            }

            asset = candidateAsset;
            break;
        }

        if (asset == null)
        {
            return;
        }

        if (GitHubAssets.IsUpToDate(gmod, asset.Sha256))
        {
            return;
        }

        var uri = new Uri(asset.DownloadUrl);
        var httpClient = new HttpClient();
        await using var s = await httpClient.GetStreamAsync(uri);
        await using var fs = new FileStream(gmod, FileMode.Create);
        await s.CopyToAsync(fs);
    }
    public static async Task CheckForGwExeUpdates(bool messageIfUpToDate, bool showCheckingDialog)
    {
        List<Account> accsToUpdate = new();
        List<Account> failedToCheck = new();
        Exception? checkError = null;
        var cancelled = false;

        if (showCheckingDialog)
        {
            using var cts = new CancellationTokenSource();
            using var checkingDialog = new Form
            {
                Text = "GW Update",
                Size = new System.Drawing.Size(300, 120),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var label = new System.Windows.Forms.Label
            {
                Text = "Checking for updates...",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            var cancelBtn = new Button
            {
                Text = "Cancel",
                Dock = DockStyle.Bottom,
                DialogResult = DialogResult.Cancel
            };
            cancelBtn.Click += (_, _) => cts.Cancel();
            checkingDialog.Controls.Add(label);
            checkingDialog.Controls.Add(cancelBtn);

            var checkCompleted = false;
            checkingDialog.FormClosing += (_, _) =>
            {
                if (!checkCompleted && !cts.IsCancellationRequested)
                    cts.Cancel();
            };

            Task? checkTask = null;
            checkingDialog.Shown += (_, _) => checkTask = Task.Run(async () =>
            {
                try
                {
                    (accsToUpdate, failedToCheck) = await RunUpdateCheck(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // User cancelled — exit silently.
                }
                catch (Exception ex)
                {
                    checkError = ex;
                }
                finally
                {
                    checkCompleted = true;
                    try { checkingDialog.Invoke(() => checkingDialog.Close()); }
                    catch { /* dialog may already be disposed */ }
                }
            });

            checkingDialog.ShowDialog();
            if (checkTask != null)
                await checkTask;

            cancelled = cts.IsCancellationRequested;
        }
        else
        {
            // Automatically triggered: check silently in the background.
            try
            {
                (accsToUpdate, failedToCheck) = await Task.Run(() => RunUpdateCheck(CancellationToken.None));
            }
            catch (Exception ex)
            {
                checkError = ex;
            }
        }

        if (checkError != null)
        {
            Console.WriteLine($"Error checking for Gw.exe updates: {checkError.Message}");
            MessageBox.Show($"Error checking for updates: {checkError.Message}", "Update Check Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (cancelled)
            return;

        if (failedToCheck.Count != 0 && messageIfUpToDate)
        {
            var failedAccNames = string.Join(',', failedToCheck.Select(acc => acc.Name));
            MessageBox.Show($"Failed to check the version number on these accounts:\n{failedAccNames}", "GW Update");
        }
        if (accsToUpdate.Count == 0)
        {
            if (messageIfUpToDate)
                MessageBox.Show("No accounts are out of date.", "GW Update");
            return;
        }
        var accNames = string.Join(',', accsToUpdate.Select(acc => acc.Name));
        var ok = MessageBox.Show($"These Accounts are out-of-date:\n{accNames}\nUpdate now?", "GW Update",
            MessageBoxButtons.YesNo);

        if (ok != DialogResult.Yes)
            return;

        AdminAccess.RestartAsAdminPrompt(true);
        if (_mainForm is not null)
        {
            await _mainForm.Invoke(async () =>
            {
                try
                {
                    Mutex.WaitOne();
                    await _mainForm.UpdateAccountsGui(accsToUpdate);
                }
                finally
                {
                    Mutex.ReleaseMutex();
                }
            });
        }
    }

    private static async Task<(List<Account> toUpdate, List<Account> failedToCheck)> RunUpdateCheck(CancellationToken ct)
    {
        var (response, error) = await GwDownloader.GetLatestGwExeInfoAsync();
        ct.ThrowIfCancellationRequested();

        if (response == null || !string.IsNullOrEmpty(error))
        {
            if (!string.IsNullOrEmpty(error))
                Console.WriteLine($"Error getting latest GW exe info: {error}");
            return (new List<Account>(), new List<Account>());
        }

        var latestFileId = response.Value.FileId;
        var accsToUpdate = new List<Account>();
        var addsFailedToCheck = new List<Account>();

        // Group accounts by exe path so each distinct exe is only parsed once,
        // then check all unique paths in parallel.
        var accountsByPath = Accounts
            .GroupBy(a => a.gwpath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var checkTasks = accountsByPath.Select(async group =>
        {
            ct.ThrowIfCancellationRequested();
            var gwpath = group.Key;
            var accountsForPath = group.ToList();

            if (!File.Exists(gwpath))
                return (toUpdate: new List<Account>(), failedToCheck: new List<Account>());

            try
            {
                var fileId = await Task.Run(() => new GuildWarsExecutableParser(gwpath).GetFileId(), ct);
                if (fileId == 0)
                    return (toUpdate: new List<Account>(), failedToCheck: accountsForPath);
                if (fileId == latestFileId)
                    return (toUpdate: new List<Account>(), failedToCheck: new List<Account>());
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error checking version for {gwpath}: {e}");
                return (toUpdate: new List<Account>(), failedToCheck: accountsForPath);
            }

            return (toUpdate: accountsForPath, failedToCheck: new List<Account>());
        });

        var results = await Task.WhenAll(checkTasks);
        foreach (var (toUpdate, failedToCheck) in results)
        {
            accsToUpdate.AddRange(toUpdate);
            addsFailedToCheck.AddRange(failedToCheck);
        }

        return (accsToUpdate, addsFailedToCheck);
    }
}
