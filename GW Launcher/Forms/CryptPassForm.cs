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
        private void CryptPassForm_Load(object sender, EventArgs e)
        {
            
        }

        private void Finish()
        {
            if (textBoxPass.Text == "") return;
            SHA256 sha = new SHA256Cng();
            Password = sha.ComputeHash(Encoding.UTF8.GetBytes(textBoxPass.Text));
            Close();
        }

        private void buttonEnter_Click(object sender, EventArgs e)
        {
            Finish();
        }

        private void textBoxPass_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 0x0D) // Enter key
            {
                Finish();
            }
        }
    }
}
