﻿using Microsoft.Win32;

namespace GW_Launcher.Forms
{
    public partial class AddAccountForm : Form
    {
        public Account account;
        public bool finished = false;

        public AddAccountForm()
        {
            account = new Account();
            InitializeComponent();
        }

        private void buttonAdd_Click(object sender, EventArgs e)
        {
            account.email = textBoxEmail.Text;
            account.password = textBoxPassword.Text;
            account.character = textBoxCharacter.Text;
            account.gwpath = textBoxPath.Text;
            account.datfix = checkBoxDatFix.Checked;
            account.elevated = checkBoxElevated.Checked;
            account.extraargs = textBoxExtraArguments.Text;
            finished = true;
            Close();
        }

        private void AddAccountForm_Load(object sender, EventArgs e)
        {
            textBoxEmail.Text = account.email;
            textBoxPassword.Text = account.password;
            textBoxCharacter.Text = account.character;
            textBoxPath.Text = account.gwpath;
            checkBoxDatFix.Checked = account.datfix;
            checkBoxElevated.Checked = account.elevated;
            textBoxExtraArguments.Text = account.extraargs;
        }

        private void buttonDialogPath_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();

            var pathdefault = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", null);
            if (pathdefault == null)
            {
                pathdefault = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars", "Path", null);
                if (pathdefault == null)
                    MessageBox.Show("pathdefault = null, gw not installed?");
            }

            openFileDialog.InitialDirectory = pathdefault;
            openFileDialog.Filter = "Guild Wars|Gw.exe";
            openFileDialog.RestoreDirectory = true;

            if(openFileDialog.ShowDialog() == DialogResult.OK)
            {
                textBoxPath.Text = openFileDialog.FileName;
            }

        }

        private void buttonShowPassword_Click(object sender, EventArgs e)
        {
            if (textBoxPassword.PasswordChar == '\0')
                textBoxPassword.PasswordChar = '*';
            else
                textBoxPassword.PasswordChar = '\0';
        }

        private void buttonMods_Click(object sender, EventArgs e)
        {
            Program.mutex.WaitOne();
            if (string.IsNullOrEmpty(account.email)) return;

            var modForm = new ModManager(account);
            modForm.Show();
        }

        private void checkBoxDatFix_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxDatFix.Checked)
            {
                MessageBox.Show(@"This is a legacy feature that will likely lead to issues. Only enable this is you know what you're doing.");
            }
        }
    }
}
