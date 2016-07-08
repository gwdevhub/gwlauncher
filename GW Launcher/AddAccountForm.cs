using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Diagnostics;
using GWCA.Memory;


namespace GW_Launcher
{
    public partial class AddAccountForm : Form
    {
        public Account result;

        public AddAccountForm()
        {
            InitializeComponent();
        }

        private void buttonDone_Click(object sender, EventArgs e)
        {
            result.email = textBoxEmail.Text;
            result.password = textBoxPassword.Text;
            result.character = textBoxChar.Text;
            result.gwpath = textBoxPath.Text;
            result.datfix = checkBoxDatFix.Checked;

            this.Close();
        }

        private void AddAccountForm_Load(object sender, EventArgs e)
        {
        }

        private void buttonDialogPath_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            var pathdefault = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", null);
            if (pathdefault == null)
            {
                pathdefault = (string)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path", null);
                if (pathdefault == null)
                    MessageBox.Show("pathdefault = null, gw not installed?");
            }

            openFileDialog.InitialDirectory = (string)pathdefault;
            openFileDialog.Filter = "Guild Wars|Gw.exe";
            openFileDialog.RestoreDirectory = true;

            if(openFileDialog.ShowDialog() == DialogResult.OK)
            {
                textBoxPath.Text = openFileDialog.FileName;
            }

        }
    }
}
