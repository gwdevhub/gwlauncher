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



    public struct Account
    {
        public string alias;
        public string email;
        public string password;
        public string character;
        public string gwpath;
        public bool datfix;
        public string extraargs;
    }



    public partial class MainForm : Form
    {
        public Account[] accounts;
        public Process[] procs;

        bool first = true;

        int heightofgui = 143;

        public int batch_index;
        ListView.SelectedIndexCollection selectedItems;

        System.Windows.Forms.Timer StatusUpdater = new System.Windows.Forms.Timer();
        System.Windows.Forms.Timer BatchLoader = new System.Windows.Forms.Timer();

        public MainForm()
        {
            InitializeComponent();
        }

        private void TimerBatchLoadAccounts(Object obj,EventArgs args)
        {
            var acc = accounts[selectedItems[batch_index]];
            listViewAccounts.Items[selectedItems[batch_index]].SubItems[3].Text = "Loading...";
            procs[batch_index] = MulticlientPatch.LaunchClient(acc.gwpath, " -email " + acc.email + " -password " + acc.password + " -character \"" + acc.character + "\" " + acc.extraargs, acc.datfix);
            listViewAccounts.Items[selectedItems[batch_index]].SubItems[3].Text = "Active";
            batch_index++;
            if(batch_index >= listViewAccounts.SelectedIndices.Count)
            {
                BatchLoader.Stop();
            }
        }

        private void TimerEventProcessor(Object myObject,
                                            EventArgs myEventArgs)
        {
            for (int i = 0; i < procs.Length; ++i)
            {
                if (procs[i] != null && procs[i].HasExited)
                {
                    listViewAccounts.Items[i].SubItems[3].Text = "Inactive";
                    procs[i] = null;
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            heightofgui = 143;
            ImageList imglist = new ImageList();

            imglist.Images.Add("gw-icon", Properties.Resources.gw_icon);

            listViewAccounts.SmallImageList = imglist;

            StreamReader file;
            try {
                file = new StreamReader("Accounts.json");
            } catch(FileNotFoundException)
            {
                StreamWriter writerfile = File.CreateText("Accounts.json");
                writerfile.Write("[]");
                writerfile.Close();
                file = new StreamReader("Accounts.json");
            }

            JsonTextReader reader = new JsonTextReader(file);
            JsonSerializer serializer = new JsonSerializer();

            accounts = serializer.Deserialize<Account[]>(reader);

            file.Close();

            procs = new Process[accounts.Length];
            bool[] alreadyonline = new bool[accounts.Length];

            Process[] gwprocs = Process.GetProcessesByName("Gw");
            foreach (Process proc in gwprocs)
            {
                GWCAMemory mem = new GWCAMemory(proc);

                string curaddr = mem.ReadWString(GWMem.EmailAddPtr, 100);
                for (int i = 0; i < accounts.Length; ++i)
                {
                    if(accounts[i].email == curaddr)
                    {
                        procs[i] = proc;
                        alreadyonline[i] = true;
                    }
                }

            }

            if (first)
            {
                StatusUpdater.Interval = 1000;
                StatusUpdater.Tick += new EventHandler(TimerEventProcessor);
                StatusUpdater.Start();

                BatchLoader.Interval = 7000;
                BatchLoader.Tick += new EventHandler(TimerBatchLoadAccounts);
            }

            if(accounts.Length > 4)
            {
                heightofgui += 17 * (accounts.Length - 4);
                this.SetBounds(Location.X, Location.Y, Size.Width, heightofgui);
            }

            for (int i = 0; i < accounts.Length; ++i)
            {
                listViewAccounts.Items.Add(new ListViewItem(new string[] { null, accounts[i].alias, accounts[i].character, alreadyonline[i] ? "Active" : "Inactive" }, "gw-icon"));
            }
            first = false;

        }

        private void listViewAccounts_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var selectedItems = listViewAccounts.SelectedIndices;
            if (selectedItems.Count == 0) return;
            var acc = accounts[selectedItems[0]];

            if (listViewAccounts.Items[selectedItems[0]].SubItems[3].Text == "Active") return;

            listViewAccounts.Items[selectedItems[0]].SubItems[3].Text = "Loading...";

            procs[selectedItems[0]] = MulticlientPatch.LaunchClient(acc.gwpath, " -email " + acc.email + " -password " + acc.password + " -character \"" + acc.character + "\" " + acc.extraargs, acc.datfix);

            new GWCAMemory(procs[selectedItems[0]]).WriteWString(GW_Launcher.GWMem.WinTitle, acc.character + '\0');

            listViewAccounts.Items[selectedItems[0]].SubItems[3].Text = "Active";
        }

        private void launchSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            selectedItems = listViewAccounts.SelectedIndices;
            if (selectedItems.Count == 0) return;
            batch_index = 0;
            BatchLoader.Start();
            TimerBatchLoadAccounts(null, new EventArgs());
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

                this.listViewAccounts.Items.Clear();
                this.OnLoad(new EventArgs());
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

            this.listViewAccounts.Items.Clear();
            this.OnLoad(new EventArgs());
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
            this.listViewAccounts.Items.Clear();
            this.OnLoad(new EventArgs());
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
            addaccform.buttonDone.Text = "Edit";
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
                this.listViewAccounts.Items.Clear();
                this.OnLoad(new EventArgs());
            }

        }
    }
}
