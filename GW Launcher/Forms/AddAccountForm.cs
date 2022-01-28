using System;
using Microsoft.Win32;
using System.Windows.Forms;


namespace GW_Launcher
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

        private void buttonDone_Click(object sender, EventArgs e)
        {
            account.email = textBoxEmail.Text;
            account.password = textBoxPassword.Text;
            account.character = textBoxChar.Text;
            account.gwpath = textBoxPath.Text;
            account.datfix = checkBoxDatFix.Checked;
            account.elevated = checkBoxElevated.Checked;
            account.extraargs = textBoxExArgs.Text;
            finished = true;
            this.Close();
        }

        private void AddAccountForm_Load(object sender, EventArgs e)
        {
            textBoxEmail.Text = account.email;
            textBoxPassword.Text = account.password;
            textBoxChar.Text = account.character;
            textBoxPath.Text = account.gwpath;
            checkBoxDatFix.Checked = account.datfix;
            checkBoxElevated.Checked = account.elevated;
            textBoxExArgs.Text = account.extraargs;
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

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBoxPassword.PasswordChar == '\0')
                textBoxPassword.PasswordChar = '*';
            else
                textBoxPassword.PasswordChar = '\0';
        }
    }
}
