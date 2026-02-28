using GW_Launcher.Forms;
using GW_Launcher.Guildwars;
using Octokit;
using Account = GW_Launcher.Classes.Account;
using Application = System.Windows.Forms.Application;
using Assembly = System.Reflection.Assembly;
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

    private static Queue<int> _needtolaunch = new Queue<int>();

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

	private static string? LaunchAccount(string accountName)
    {
        var found = Accounts.IndexOf(accountName);
        if (found == -1)
            return "Failed to find account for " + accountName;
		var result = DeleteSteamAppIdFile(Accounts[found]);
		if (result != null) return result;
		result = CreateSteamAppIdFile(Accounts[found]);
        if (result != null) return result;
		result = LaunchAccount(Accounts[found]);
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

    private static string? LaunchAccount(Account account)
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

            var res = MulticlientPatch.LaunchClient(account, out memory);
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
            Task.Run(async () => await CheckForGwExeUpdates(false));
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
                    var accountName = GetAccountName(_needtolaunch.Dequeue());

                    var res = LaunchAccount(accountName);
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

    public static bool QueueLaunch(int index)
    {
        if (index < 0 || Accounts.Length <= index)
            return false;
        _needtolaunch.Enqueue(index);
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
        catch (Exception)
        {
            if (Settings.Encrypt) return false; // error message already shown
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
        const string oldName = ".old.exe";
        const string newName = ".new.exe";
        if (Settings.AutoUpdate && (File.Exists(oldName) || File.Exists(newName)))
        {
            File.Delete(oldName);
            File.Delete(newName);
        }

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
        var runtimeInstalled = IsDotNet8DesktopInstalled();

        if (!Settings.AutoUpdate)
        {
            var msgBoxResult = MessageBox.Show(
                $@"New version {tagName} available online. Visit page?{(runtimeInstalled ? "\nYou can download the Framework Dependent version." : "")}",
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

        var assetName = runtimeInstalled
            ? "GW_Launcher_Framework_Dependent.exe"
            : "GW_Launcher.exe";

        var asset = latest.Assets.FirstOrDefault(a => a.Name == assetName);
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

        Mutex.WaitOne();
        ShouldClose = true;
        if (Mainthread.ThreadState == ThreadState.Running && !Mainthread.Join(5000))
        {
            return;
        }

        Mutex.Close();
        GwlMutex?.Close();

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

    private static bool IsDotNet8DesktopInstalled()
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

            return output.Contains("Microsoft.WindowsDesktop.App 8");
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
	public static async Task CheckForGwExeUpdates(bool messageIfUpToDate)
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
		checkingDialog.FormClosing += (_, e) =>
		{
			if (!cts.IsCancellationRequested)
				cts.Cancel();
		};

		// Run the check in the background, close dialog when done
		var checkTask = Task.Run(async () =>
		{
			try
			{
				await RunUpdateCheck(messageIfUpToDate, cts.Token);
			}
			finally
			{
				checkingDialog.Invoke(() => checkingDialog.Close());
			}
		}, cts.Token);

		checkingDialog.ShowDialog();
		await checkTask; // propagate exceptions / await completion
	}

	private static async Task RunUpdateCheck(bool messageIfUpToDate, CancellationToken ct)
	{
		try
		{
			var (response, error) = await GwDownloader.GetLatestGwExeInfoAsync();
			ct.ThrowIfCancellationRequested();

			if (response == null || !string.IsNullOrEmpty(error))
			{
				if (!string.IsNullOrEmpty(error))
					Console.WriteLine($"Error getting latest GW exe info: {error}");
				return;
			}

			var latestFileId = response.Value.FileId;
			var accsToUpdate = new List<Account>();
			var accsChecked = new List<Account>();

			foreach (var account in Accounts)
			{
				ct.ThrowIfCancellationRequested();

				if (accsChecked.Select(a => a.gwpath).Contains(account.gwpath)) continue;
				if (!File.Exists(account.gwpath))
				{
					accsToUpdate.Add(account);
					continue;
				}
				accsChecked.Add(account);
				try
				{
					var parser = new GuildWarsExecutableParser(account.gwpath);
					if (parser.GetFileId() == latestFileId)
						continue;
				}
				catch (Exception e)
				{
					Console.WriteLine($"Error checking version for {account.gwpath}: {e}");
					continue;
				}
				accsToUpdate.Add(account);
			}

			if (accsToUpdate.Count == 0)
			{
				if (messageIfUpToDate)
					MessageBox.Show("No accounts are out of date.", "GW Update");
				return;
			}

			var accNames = string.Join(',', accsToUpdate.Select(acc => acc.Name));
			var ok = MessageBox.Show($"Accounts {accNames} are out of date. Update now?", "GW Update",
				MessageBoxButtons.YesNo);

			if (ok == DialogResult.Yes)
			{
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
		}
		catch (OperationCanceledException)
		{
			// User cancelled — exit silently
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error checking for Gw.exe updates: {ex.Message}");
			MessageBox.Show($"Error checking for updates: {ex.Message}", "Update Check Error",
				MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}
}
