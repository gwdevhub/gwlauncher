using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.IO;
using GWCA.Memory;
using GWMC_CS;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GW_Launcher
{
    public partial class MainForm : Form
    {
        public Queue<int> needtolaunch;

        int heightofgui = 143;

        public int batch_index;
        ListView.SelectedIndexCollection selectedItems;

        System.Windows.Forms.Timer StatusUpdater = new System.Windows.Forms.Timer();
        System.Windows.Forms.Timer BatchLoader = new System.Windows.Forms.Timer();

        Point detachedPosition = new Point(400,400);

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
                this.SetBounds(Location.X, Location.Y, Size.Width, heightofgui);
            }
            this.listViewAccounts.Items.Clear();

            // Run through already open GW clients to see if accounts are already active.
            foreach (Process p in Process.GetProcessesByName("Gw"))
            {
                GWCAMemory m = new GWCAMemory(p);
                GWMem.FindAddressesIfNeeded(m);
                string str = m.ReadWString(GWMem.EmailAddPtr, 64);
                for (int i = 0; i < Program.accounts.Length; ++i)
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
            for (int i = 0; i < Program.accounts.Length; ++i)
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
            if(this.listViewAccounts.InvokeRequired)
            {
                SetActiveUICallback cb = new SetActiveUICallback(SetActive);
                this.Invoke(cb, new object[] { idx, active });
            }
            else
            {
                Program.accounts[idx].active = active;
                listViewAccounts.Items[idx].SubItems[1].Text = active ? "Active" : "Inactive";
            }    
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Visible = false;
            // Initialize things
            ImageList imglist = new ImageList();
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
            AddAccountForm gui = new AddAccountForm();
            gui.ShowDialog();
            Account acc = gui.account;

            if (acc.email != null)
            {
                Account[] new_accs = new Account[Program.accounts.Length + 1];
                Program.accounts.CopyTo(new_accs, 0);
                new_accs[Program.accounts.Length] = acc;
                Program.accounts = new_accs;

                using (StreamWriter sw = new StreamWriter("Accounts.json"))
                {
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, Program.accounts);
                    }
                }

                RefreshUI();
            }
            Program.mutex.ReleaseMutex();
        }

        private void removeSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.mutex.WaitOne();
            var selectedthing = listViewAccounts.SelectedIndices[0];
            var account = Program.accounts[selectedthing];
            Account[] new_accs = new Account[Program.accounts.Length - 1];
            int j = 0;
            for(int i = 0; i < Program.accounts.Length; ++i)
            {
                if (Program.accounts[i].email != account.email)
                {
                    new_accs[j] = Program.accounts[i];
                    j++;
                }
            }
            Program.accounts = new_accs;

            using (StreamWriter sw = new StreamWriter("Accounts.json"))
            {
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    jw.Formatting = Formatting.Indented;
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(jw, Program.accounts);
                }
            }

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
            Program.accounts = JsonConvert.DeserializeObject<Account[]>(File.ReadAllText("Accounts.json"));
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
            if (acc.email == "") return;

            var addaccform = new AddAccountForm();
            addaccform.Text = "Modify Account";
            addaccform.account = acc;
            addaccform.ShowDialog();

            if (addaccform.finished)
            {
                Program.mutex.WaitOne();
                Program.accounts[idx] = addaccform.account;
                using (StreamWriter sw = new StreamWriter("Accounts.json"))
                {
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, Program.accounts);
                    }
                }
                Program.mutex.ReleaseMutex();
            }
           
        }

        private void listViewAccounts_ItemDrag(object sender, ItemDragEventArgs e)
        {
            
        }

        private void MainForm_Deactivate(object sender, EventArgs e)
        {
            if(!this.rightClickOpen)
                this.Visible = false;
        }

        private void notifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if(this.rightClickOpen)
                {
                    this.Visible = false;
                    this.rightClickOpen = false;
                    return;
                }
                this.rightClickOpen = true;
            }

            Point loc = Cursor.Position;

            loc.X -= (this.Width / 2);

            loc.Y -= (25 + this.Height);

            this.Location = loc;

            this.Visible = !this.Visible;
            this.Activate();
        }
    }
}
