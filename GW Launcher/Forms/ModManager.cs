using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.IO;

namespace GW_Launcher
{
    public partial class ModManager : Form
    {
        private Account account;

        public ModManager(Account account)
        {
            this.account = account;

            InitializeComponent();

            this.Text = "Mod Manager for " + this.account.character;
        }

        private void TexmodManager_Load(object sender, EventArgs e)
        {
            if(account.mods == null)
            {
                return;
            }

            foreach (var mod in account.mods)
            {
                string name = mod.fileName.Split('\\').Last();
                ListViewItem item = new ListViewItem(name, mod.fileName);

                item.Checked = mod.active;

                switch (mod.type)
                {
                    case ModType.kModTypeTexmod:
                        listViewAvailableMods.Groups[0].Items.Add(item);
                        break;

                    case ModType.kModTypeDLL:
                        listViewAvailableMods.Groups[1].Items.Add(item);
                        break;
                }
                listViewAvailableMods.Items.Add(item);
            }
        }

        private void listViewAvailableMods_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            var mod = account.mods[e.Item.Index];
            mod.active = e.Item.Checked;
            Program.accounts.Save();
        }

        private void addModToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select Mod File to Use";
            openFileDialog.Filter = "Mod Files (*.dll;*.zip;*.tpf)|*.dll;*.zip;*.tpf|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Mod mod = new Mod();
                mod.fileName = openFileDialog.FileName;
                mod.active = false;
                switch (openFileDialog.FileName.Split('.').Last())
                {
                    case "dll":
                        mod.type = ModType.kModTypeDLL;
                        break;
                    case "zip":
                    case "tpf":
                        mod.type = ModType.kModTypeTexmod;
                        break;
                }
                if (account.mods == null)
                    account.mods = new List<Mod>();
                account.mods.Add(mod);
                Program.accounts.Save();
            }
        }

        private void removeSelectedToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }
    }
}
