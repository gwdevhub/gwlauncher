using GW_Launcher.Memory;
using Microsoft.Win32;

namespace GW_Launcher.Forms;

public partial class MainForm : Form
{
    public Queue<int> needtolaunch;

    int heightofgui = 143;

    ListView.SelectedIndexCollection selectedItems;

    bool rightClickOpen = false;

    public MainForm()
    {
        InitializeComponent();
        needtolaunch = new Queue<int>();
        selectedItems = new ListView.SelectedIndexCollection(listViewAccounts);
    }

    private void RefreshUI()
    {
        if (Program.accounts.Length > 4)
        {
            heightofgui = 143 + 17 * (Program.accounts.Length - 4);
            SetBounds(Location.X, Location.Y, Size.Width, heightofgui);
        }
        listViewAccounts.Items.Clear();

        // Run through already open GW clients to see if accounts are already active.
        foreach (var process in Process.GetProcessesByName("Gw"))
        {
            if (process.Threads.Count == 1)
                continue;
            try
            {
                var memory = new GWCAMemory(process);
                GWMemory.FindAddressesIfNeeded(memory);
                var email = memory.ReadWString(GWMemory.EmailAddPtr, 64);
                foreach (var account in Program.accounts)
                {
                    if (email != account.email) continue;
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
                    AdminAccess.RestartAsAdminPrompt(true);
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
                new[] {
                    account.Name,
                    account.active ? "Active" : "Inactive"
                },
                "gw-icon"
            ));
        }
    }

    delegate void SetActiveUICallback(int index, bool active);

    public void SetActive(int index, bool active)
    {
        if(listViewAccounts.InvokeRequired)
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
        needtolaunch = new Queue<int>();
        imageList.Images.Add("gw-icon", Properties.Resources.gw_icon);
        listViewAccounts.SmallImageList = imageList;
        RefreshUI();
        Program.mainthread.Start();
    }

    private void ListViewAccounts_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        var selectedItems = listViewAccounts.SelectedIndices;
        if (selectedItems.Count == 0) return;
        needtolaunch.Enqueue(selectedItems[0]);
    }

    private void ToolStripMenuItemLaunchSelected_Click(object sender, EventArgs e)
    {
        selectedItems = listViewAccounts.SelectedIndices;
        if (selectedItems.Count == 0) return;
        foreach(int selectedItem in selectedItems)
        {
            needtolaunch.Enqueue(selectedItem);
        }
    }

    private void ToolStripMenuItemAddNew_Click(object sender, EventArgs e)
    {
        Program.mutex.WaitOne();
        var gui = new AddAccountForm();
        gui.ShowDialog();
        var account = gui.account;
        if (account.email != null)
        {
            Program.accounts.Add(account);
            Program.accounts.Save();
            RefreshUI();
        }

        Program.mutex.ReleaseMutex();
    }

    private void ToolStripMenuItemRemoveSelected_Click(object sender, EventArgs e)
    {
        Program.mutex.WaitOne();
        var indices  = from int indice in listViewAccounts.SelectedIndices orderby indice descending select indice;
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
        var pathdefault = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", null);
        if (pathdefault == null)
        {
            pathdefault = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path", null);
            if (pathdefault == null)
                MessageBox.Show(@"pathdefault = null, gw not installed?");
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
        Program.mutex.WaitOne();
        selectedItems = listViewAccounts.SelectedIndices;
        if (selectedItems.Count == 0 && listViewAccounts.FocusedItem == null)
            return;

        int? index = selectedItems.Contains(listViewAccounts.FocusedItem.Index) ? listViewAccounts.FocusedItem.Index : null;
        if (index == null && selectedItems.Count > 0)
        {
            index = selectedItems[0];
        }

        if (index == null) return;
        var account = Program.accounts[(int) index];
        var addAccountForm = new AddAccountForm
        {
            Text = @"Modify Account",
            account = account
        };

        addAccountForm.ShowDialog();
        if (addAccountForm.finished)
        {
            Program.accounts[(int) index] = addAccountForm.account;
            RefreshUI();
        }

        Program.mutex.ReleaseMutex();
    }

    private void MainForm_Deactivate(object sender, EventArgs e)
    {
        if(!rightClickOpen)
            Visible = false;
    }

    private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            if(rightClickOpen)
            {
                Visible = false;
                rightClickOpen = false;
                return;
            }

            rightClickOpen = true;
        }

        var isVisible = (Point p) =>
        {
            return Screen.AllScreens.Any(s => p.X < s.Bounds.Right && p.X > s.Bounds.Left && p.Y > s.Bounds.Top && p.Y < s.Bounds.Bottom);
        };

        var position = Cursor.Position;

        position.X -= (Width / 2);
        if (position.Y > (SystemInformation.VirtualScreen.Height / 2))
        {
            position.Y -= (25 + Height);
        }
        else
        {
            position.Y += 25;
        }

        if (!isVisible(position))
        {
            position.Y = Cursor.Position.Y;
        }

        if (!isVisible(position))
        {
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
                File.Delete(tmpfile);
            }

            var process = Process.Start(client, "-image");
            var taskCompletionSource = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => taskCompletionSource.TrySetResult(null!);
            if (cancellationToken != default)
                cancellationToken.Register(taskCompletionSource.SetCanceled);

            if (File.Exists(tmpfile))
            {
                File.Delete(tmpfile);
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
        if (!AdminAccess.HasAdmin())
        {
            if (!AdminAccess.RestartAsAdminPrompt())
            {
                return;
            }
        }
        var clients = Program.accounts.Select(account => account.gwpath).Distinct();

        foreach (var client in clients)
        {
            await RunClientUpdateAsync(client);
        }
    }
}
