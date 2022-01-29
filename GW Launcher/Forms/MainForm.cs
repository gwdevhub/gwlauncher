using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO;
using GWCA.Memory;
using GWMC_CS;
using Microsoft.Win32;
using System.Security.Principal;
using System.Reflection;

namespace GW_Launcher
{
    public partial class MainForm : Form
    {
        public Queue<int> needtolaunch;

        int heightofgui = 143;

        public int batch_index;
        ListView.SelectedIndexCollection selectedItems;

        bool rightClickOpen = false;

        public MainForm()
        {
            InitializeComponent();
            
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
            foreach (var p in Process.GetProcessesByName("Gw"))
            {
                if (p.Threads.Count == 1)
                    continue;
                var m = new GWCAMemory(p);
                GWMem.FindAddressesIfNeeded(m);
                var str = m.ReadWString(GWMem.EmailAddPtr, 64);
                for (var i = 0; i < Program.accounts.Length; ++i)
                {
                    if (str == Program.accounts[i].email)
                    {
                        Program.accounts[i].active = true;
                        Program.accounts[i].process = m;
                        break;
                    }
                }
            }

            // Fill out data.
            for (var i = 0; i < Program.accounts.Length; ++i)
            {
                listViewAccounts.Items.Add(new ListViewItem(
                    new string[] {
                            Program.accounts[i].character,
                            Program.accounts[i].active ? "Active" : "Inactive"
                    },
                    "gw-icon"
                    ));

            }
        }

        delegate void SetActiveUICallback(int idx, bool active);

        public void SetActive(int idx, bool active)
        {
            if(listViewAccounts.InvokeRequired)
            {
                var cb = new SetActiveUICallback(SetActive);
                Invoke(cb, new object[] { idx, active });
            }
            else
            {
                Program.accounts[idx].active = active;
                listViewAccounts.Items[idx].SubItems[1].Text = active ? "Active" : "Inactive";
            }    
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            Visible = false;
            // Initialize things
            var imglist = new ImageList();
            needtolaunch = new Queue<int>();
            imglist.Images.Add("gw-icon", Properties.Resources.gw_icon);
            listViewAccounts.SmallImageList = imglist;
            RefreshUI();
            Program.mainthread.Start();
        }

        private void listViewAccounts_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var selectedItems = listViewAccounts.SelectedIndices;
            if (selectedItems.Count == 0) return;
            needtolaunch.Enqueue(selectedItems[0]);
        }

        private void launchSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectedItems = listViewAccounts.SelectedIndices;
            if (selectedItems.Count == 0) return;
            foreach(int i in selectedItems)
            {
                needtolaunch.Enqueue(i);
            }
        }

        private void addNewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.mutex.WaitOne();
            var gui = new AddAccountForm();
            gui.ShowDialog();
            var acc = gui.account;

            if (acc.email != null)
            {
                Program.accounts.Add(acc);
                Program.accounts.Save();
                RefreshUI();
            }
            Program.mutex.ReleaseMutex();
        }

        private void removeSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.mutex.WaitOne();
            var selectedthing = listViewAccounts.SelectedIndices[0];
            var account = Program.accounts[selectedthing];
            Program.accounts.Remove(account.email);
            Program.accounts.Save();
            RefreshUI();
            Program.mutex.ReleaseMutex();
        }

        private void launchGWInstanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var pathdefault = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", null);
            if (pathdefault == null)
            {
                pathdefault = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path", null);
                if (pathdefault == null)
                    MessageBox.Show("pathdefault = null, gw not installed?");
            }
            MulticlientPatch.LaunchClient(pathdefault, "", true, true);
        }

        private void refreshAccountsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.mutex.WaitOne();
            Program.accounts.Load("Accounts.json");
            RefreshUI();
            Program.mutex.ReleaseMutex();
        }

        private void editSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.mutex.WaitOne();
            selectedItems = listViewAccounts.SelectedIndices;
            if (selectedItems.Count == 0) return;
            var idx = selectedItems[0];
            var acc = Program.accounts[idx];

            var addaccform = new AddAccountForm
            {
                Text = @"Modify Account",
                account = acc
            };
            addaccform.ShowDialog();

            if (addaccform.finished)
            {
                Program.accounts[idx] = addaccform.account;
            }
            Program.mutex.ReleaseMutex();
        }

        private void texmodsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.mutex.WaitOne();
            selectedItems = listViewAccounts.SelectedIndices;
            if (selectedItems.Count == 0) return;
            var idx = selectedItems[0];
            var acc = Program.accounts[idx];
            if (string.IsNullOrEmpty(acc.email)) return;

            var modForm = new ModManager(acc);
            modForm.Show();
        }

        private void listViewAccounts_ItemDrag(object sender, ItemDragEventArgs e)
        {
            
        }

        private void MainForm_Deactivate(object sender, EventArgs e)
        {
            if(!rightClickOpen)
                Visible = false;
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
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

            var loc = Cursor.Position;

            loc.X -= (Width / 2);
            if (loc.Y > (SystemInformation.VirtualScreen.Height / 2))
            {
                loc.Y -= (25 + Height);
            }
            else
            {
                loc.Y += 25;
            }

            Location = loc;

            Visible = !Visible;
            Activate();
        }


        private Task RunClientUpdateAsync(string client, CancellationToken cancellationToken = default)
        {
            try
            {
                var tmpfile = Path.GetDirectoryName(client) + Path.DirectorySeparatorChar + "Gw.tmp";
                if (File.Exists(tmpfile))
                {
                    File.Delete(tmpfile);
                }

                var proc = Process.Start(client, "-image");
                var tcs = new TaskCompletionSource<object>();
                proc.EnableRaisingEvents = true;
                proc.Exited += (sender, args) => tcs.TrySetResult(null);
                if (cancellationToken != default)
                    cancellationToken.Register(tcs.SetCanceled);

                if (File.Exists(tmpfile))
                {
                    File.Delete(tmpfile);
                }

                return tcs.Task;
            }
            catch(Win32Exception e) when ((uint)e.ErrorCode == 0x80004005)
            {
                return Task.CompletedTask;
            }
        }

        private async void updateAllClientsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var pricipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            var hasAdministrativeRight = pricipal.IsInRole(WindowsBuiltInRole.Administrator);
            if (!hasAdministrativeRight)
            {
                // relaunch the application with admin rights
                var fileName = Assembly.GetExecutingAssembly().Location;
                var processInfo = new ProcessStartInfo
                {
                    Verb = "runas",
                    FileName = fileName
                };

                try
                {
                    Application.Exit();
                    Process.Start(processInfo);
                }
                catch (Win32Exception)
                {
                    // This will be thrown if the user cancels the prompt
                }
                return;
            }
            var clients = Program.accounts.Select(i => i.gwpath).Distinct();

            foreach(var client in clients)
            {
                await RunClientUpdateAsync(client);
            }
        }
    }
}
