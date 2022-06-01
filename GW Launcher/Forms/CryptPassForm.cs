using System;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace GW_Launcher.Forms
{
    public partial class CryptPassForm : Form
    {
        public byte[] Password { get; private set; }

        public CryptPassForm()
        {
            InitializeComponent();
        }

        private void Finish()
        {
            if (textBoxPassword.Text == "")
                return;

            var sha = SHA256.Create();
            Password = sha.ComputeHash(Encoding.UTF8.GetBytes(textBoxPassword.Text));
            Close();
        }

        private void ButtonEnter_Click(object sender, EventArgs e)
        {
            Finish();
        }

        private void TextBoxPassword_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 0x0D) // Enter key
            {
                Finish();
            }
        }
    }
}
