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
        public Account[] accounts;

        int heightofgui = 143;

        public int batch_index;
        ListView.SelectedIndexCollection selectedItems;

        System.Windows.Forms.Timer StatusUpdater = new System.Windows.Forms.Timer();
        System.Windows.Forms.Timer BatchLoader = new System.Windows.Forms.Timer();

        public MainForm(Account[] accounts)
        {
            this.accounts = accounts;
            InitializeComponent();
        }

        private void RefreshUI()
        {
            if (accounts.Length > 4)
            {
                heightofgui = 143 + 17 * (accounts.Length - 4);
                this.SetBounds(Location.X, Location.Y, Size.Width, heightofgui);
            }
            this.listViewAccounts.Items.Clear();

            // Run through already open GW clients to see if accounts are already active.
            foreach (Process p in Process.GetProcessesByName("Gw"))
            {
                GWCAMemory m = new GWCAMemory(p);
                string str = m.ReadWString(GWMem.EmailAddPtr, 64);
                for (int i = 0; i < accounts.Length; ++i)
                {
                    if (str == accounts[i].email)
                    {
                        accounts[i].active = true;
                        Program.processes[i] = m;
                        break;
                    }
                }
            }

            // Fill out data.
            for (int i = 0; i < accounts.Length; ++i)
            {
                listViewAccounts.Items.Add(new ListViewItem(
                    new string[] {
                            accounts[i].character,
                            accounts[i].active ? "Active" : "Inactive"
                    },
                    "gw-icon"
                    ));

            }
        }

        public void SetActive(int idx, bool active)
        {
            accounts[idx].active = active;
            listViewAccounts.Items[idx].SubItems[1].Text = active ? "Active" : "Inactive";
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
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
            AddAccountForm gui = new AddAccountForm();
            gui.ShowDialog();
            Account acc = gui.account;

            if (acc.email != null)
            {
                Account[] new_accs = new Account[accounts.Length + 1];
                accounts.CopyTo(new_accs, 0);
                new_accs[accounts.Length] = acc;
                accounts = new_accs;

                using (StreamWriter sw = new StreamWriter("Accounts.json"))
                {
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, accounts);
                    }
                }

                RefreshUI();
            }
        }

        private void removeSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedthing = listViewAccounts.SelectedIndices[0];
            var account = accounts[selectedthing];
            Account[] new_accs = new Account[accounts.Length - 1];
            int j = 0;
            for(int i = 0; i < accounts.Length; ++i)
            {
                if (accounts[i].email != account.email)
                {
                    new_accs[j] = accounts[i];
                    j++;
                }
            }
            accounts = new_accs;

            using (StreamWriter sw = new StreamWriter("Accounts.json"))
            {
                using (JsonWriter jw = new JsonTextWriter(sw))
                {
                    jw.Formatting = Formatting.Indented;
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(jw, accounts);
                }
            }

            RefreshUI();
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
            RefreshUI();
        }

        private void editSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectedItems = listViewAccounts.SelectedIndices;
            if (selectedItems.Count == 0) return;
            var idx = selectedItems[0];
            var acc = accounts[idx];
            if (acc.email == "") return;

            var addaccform = new AddAccountForm();
            addaccform.Text = "Modify Account";
            addaccform.account = acc;
            addaccform.ShowDialog();

            if (addaccform.finished)
            {
                accounts[idx] = addaccform.account;
                using (StreamWriter sw = new StreamWriter("Accounts.json"))
                {
                    using (JsonWriter jw = new JsonTextWriter(sw))
                    {
                        jw.Formatting = Formatting.Indented;
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(jw, accounts);
                    }
                }
            }

        }
    }
}
