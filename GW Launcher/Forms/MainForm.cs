using GW_Launcher.Properties;
using Microsoft.Win32;

namespace GW_Launcher.Forms;

public partial class MainForm : Form
{
    public Queue<int> needtolaunch;

    private bool _keepOpen;
    private bool _allowVisible;

    private ListView.SelectedIndexCollection _selectedItems;

    private static MainForm? _instance;

    public MainForm()
    {
        InitializeComponent();
        needtolaunch = new Queue<int>();
        _selectedItems = new ListView.SelectedIndexCollection(listViewAccounts);
        _instance = this;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _instance = null;
        base.OnFormClosing(e);
    }

    public static void OnAccountSaved(Account account)
    {
        Program.mutex.WaitOne();
        var found = Program.accounts[account.guid];
        if (found != null)
        {
            Program.accounts[account.guid] = account;
        }
        else
        {
            Program.accounts.Add(account);
        }
        Program.accounts.Save();
        Program.mutex.ReleaseMutex();
        _instance?.RefreshUI();
    }

    protected override void SetVisibleCore(bool value)
    {
        if (!_allowVisible)
        {
            value = false;
            if (!IsHandleCreated) CreateHandle();
        }
        base.SetVisibleCore(value);
    }

    private void RefreshUI()
    {
        var padding = Width - listViewAccounts.Width;
        listViewAccounts.Items.Clear();

        // Run through already open GW clients to see if accounts are already active.
        foreach (var process in Process.GetProcessesByName("Gw"))
        {
            if (process.Threads.Count == 1)
            {
                continue;
            }

            try
            {
                var memory = new GWCAMemory(process);
                GWMemory.FindAddressesIfNeeded(memory);
                var email = memory.ReadWString(GWMemory.EmailAddPtr, 64);
                foreach (var account in Program.accounts)
                {
                    if (email != account.email)
                    {
                        continue;
                    }

                    account.active = true;
                    account.process = memory;
                    break;
                }
            }
            catch (Win32Exception)
            {
                if (!AdminAccess.HasAdmin())
                {
                    MessageBox.Show(
                        @"There is a running Guild Wars instance with a higher privilege level than GW Launcher currently has. Attempting to restart as Admin.");
                    if (!AdminAccess.RestartAsAdminPrompt(true))
                        return;
                }
                else
                {
                    MessageBox.Show(
                        @"Can't read memory of an open Guild Wars instance. Launcher will close.");
                    Program.shouldClose = true;
                }
            }
        }

        // Fill out data.
        foreach (var account in Program.accounts)
        {
            listViewAccounts.Items.Add(new ListViewItem(
                new[]
                {
                    account.Name,
                    account.active ? "Active" : "Inactive"
                },
                "gw-icon"
            ));
        }

        listViewAccounts.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
        listViewAccounts.Columns[0].Width = -2;
        Width = listViewAccounts.Columns[0].Width + listViewAccounts.Columns[1].Width + 5 + padding;

        var minWidth = Width - padding - listViewAccounts.Columns[1].Width - 5;
        listViewAccounts.Columns[0].Width = Math.Max(minWidth, listViewAccounts.Columns[0].Width);

        if (listViewAccounts.Items.Count <= 4)
        {
            return;
        }

        var itemHeight = listViewAccounts.GetItemRect(0).Height;
        var minHeight = 100 + itemHeight * listViewAccounts.Items.Count;

        Height = Math.Max(Height, minHeight);
    }

    public void SetActive(int index, bool active)
    {
        if (listViewAccounts.InvokeRequired)
        {
            var callback = new SetActiveUICallback(SetActive);
            Invoke(callback, index, active);
        }
        else
        {
            Program.accounts[index].active = active;
            listViewAccounts.Items[index].SubItems[1].Text = active ? "Active" : "Inactive";
        }
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        Visible = false;
        // Initialize things
        var imageList = new ImageList();
        imageList.Images.Add("gw-icon", Resources.gw_icon);
        listViewAccounts.SmallImageList = imageList;
        RefreshUI();
        Program.mainthread.Start();
    }

    private void ListViewAccounts_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        var selectedItems = listViewAccounts.SelectedIndices;
        if (selectedItems.Count == 0)
        {
            return;
        }

        needtolaunch.Enqueue(selectedItems[0]);
    }

    private void ToolStripMenuItemLaunchSelected_Click(object sender, EventArgs e)
    {
        _selectedItems = listViewAccounts.SelectedIndices;
        if (_selectedItems.Count == 0)
        {
            return;
        }

        foreach (int selectedItem in _selectedItems)
        {
            needtolaunch.Enqueue(selectedItem);
        }
    }

    private void ToolStripMenuItemAddNew_Click(object sender, EventArgs e)
    {
        using var gui = new AddAccountForm();
        gui.ShowDialog();
    }

    private void ToolStripMenuItemRemoveSelected_Click(object sender, EventArgs e)
    {
        Program.mutex.WaitOne();
        var indices = from int indice in listViewAccounts.SelectedIndices orderby indice descending select indice;
        foreach (var indice in indices)
        {
            Program.accounts.Remove(indice);
        }

        Program.accounts.Save();
        RefreshUI();
        Program.mutex.ReleaseMutex();
    }

    private void ToolStripMenuItemLaunchGWInstance_Click(object sender, EventArgs e)
    {
        var pathdefault =
            (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", null);
        if (pathdefault == null)
        {
            pathdefault = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars",
                "Path", null);
            if (pathdefault == null)
            {
                MessageBox.Show(@"Couldn't find a default installation of Guild Wars, is it installed?");
            }
        }

        if (pathdefault != null)
        {
            MulticlientPatch.LaunchClient(pathdefault);
        }
    }

    private void ToolStripMenuItemRefreshAccounts_Click(object sender, EventArgs e)
    {
        Program.mutex.WaitOne();
        Program.accounts.Load("Accounts.json");
        RefreshUI();
        Program.mutex.ReleaseMutex();
    }

    private void ToolStripMenuItemEditSelected_Click(object sender, EventArgs e)
    {
        _selectedItems = listViewAccounts.SelectedIndices;
        if (_selectedItems.Count == 0 && listViewAccounts.FocusedItem == null)
        {
            return;
        }

        int? index = listViewAccounts.FocusedItem != null && _selectedItems.Contains(listViewAccounts.FocusedItem.Index)
            ? listViewAccounts.FocusedItem.Index
            : null;
        if (index == null && _selectedItems.Count > 0)
        {
            index = _selectedItems[0];
        }

        if (index == null)
        {
            return;
        }

        var account = Program.accounts[(int)index];
        using var addAccountForm = new AddAccountForm
        {
            Text = @"Modify Account",
            account = account
        };

        addAccountForm.ShowDialog();

    }

    private void MainForm_Deactivate(object sender, EventArgs e)
    {
        if (!_keepOpen)
        {
            Visible = false;
        }
    }

    private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
    {
        _allowVisible = true;
        _keepOpen = e.Button == MouseButtons.Right && Visible == false;

        bool IsVisible(Point p)
        {
            return Screen.AllScreens.Any(s => p.X < s.Bounds.Right && p.X > s.Bounds.Left && p.Y > s.Bounds.Top && p.Y < s.Bounds.Bottom);
        }

        var rect = NotifyIconHelper.GetIconRect(notifyIcon);
        var position = new Point(rect.Left, rect.Top);

        RefreshUI();

        position.X -= Width / 2;
        if (position.Y > Screen.FromPoint(Cursor.Position).WorkingArea.Height / 2)
        {
            position.Y -= 5 + Height;
        }
        else
        {
            position.Y += 5;
        }

        if (!IsVisible(position))
        {
            position.Y = Cursor.Position.Y;
        }

        if (!IsVisible(position))
        {
            Debug.Assert(Screen.PrimaryScreen != null, "Screen.PrimaryScreen != null");
            position.X = Screen.PrimaryScreen.Bounds.Width / 2;
            position.Y = Screen.PrimaryScreen.Bounds.Height / 2;
        }

        Location = position;

        Visible = !Visible;
        Activate();
    }

    private static Task RunClientUpdateAsync(string client, CancellationToken cancellationToken = default)
    {
        try
        {
            var tmpfile = Path.GetDirectoryName(client) + Path.DirectorySeparatorChar + "Gw.tmp";
            if (File.Exists(tmpfile))
            {
                try
                {
                    File.Delete(tmpfile);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            Process process = new()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = client,
                    Arguments = "-image",
                    UseShellExecute = true,
                    Verb = "runas"
                }
            };
            process.Start();

            var taskCompletionSource = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => taskCompletionSource.TrySetResult(null!);
            if (cancellationToken != default)
            {
                cancellationToken.Register(taskCompletionSource.SetCanceled);
            }

            if (File.Exists(tmpfile))
            {
                try
                {
                    File.Delete(tmpfile);
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return taskCompletionSource.Task;
        }
        catch (Win32Exception e) when ((uint)e.ErrorCode == 0x80004005)
        {
            return Task.CompletedTask;
        }
    }

    private async void ToolStripMenuItemUpdateAllClients_Click(object sender, EventArgs e)
    {
        var clients = Program.accounts.Select(account => account.gwpath).Distinct();

        foreach (var client in clients)
        {
            await RunClientUpdateAsync(client);
        }

        Show();
    }

    private delegate void SetActiveUICallback(int index, bool active);
}
