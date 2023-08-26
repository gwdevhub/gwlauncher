using Microsoft.Win32;

namespace GW_Launcher.Forms;

public partial class AddAccountForm : Form
{
    public Account account;

    public AddAccountForm()
    {
        account = new Account();
        InitializeComponent();
    }

    private void ButtonDone_Click(object sender, EventArgs e)
    {
        SaveAccount();
    }

    private void SaveAccount()
    {
        account.title = textBoxTitle.Text;
        account.email = textBoxEmail.Text;
        account.password = textBoxPassword.Text;
        account.character = textBoxCharacter.Text;
        account.gwpath = textBoxPath.Text;
        account.elevated = checkBoxElevated.Checked;
        account.usePluginFolderMods = checkBoxUsePluginFolderMods.Checked;
        account.extraargs = textBoxExtraArguments.Text;
        MainForm.OnAccountSaved(account);
    }

    private void AddAccountForm_Load(object sender, EventArgs e)
    {
        textBoxTitle.Text = account.title;
        textBoxEmail.Text = account.email;
        textBoxPassword.Text = account.password;
        textBoxCharacter.Text = account.character;
        textBoxPath.Text = account.gwpath;
        checkBoxElevated.Checked = account.elevated;
        checkBoxUsePluginFolderMods.Checked = account.usePluginFolderMods;
        textBoxExtraArguments.Text = account.extraargs;
    }

    private void ButtonDialogPath_Click(object sender, EventArgs e)
    {
        var openFileDialog = new OpenFileDialog();

        var pathdefault =
            (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\ArenaNet\\Guild Wars", "Path", null);
        if (pathdefault == null)
        {
            pathdefault = (string?)Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\ArenaNet\\Guild Wars",
                "Path", null);
            if (pathdefault == null)
            {
                MessageBox.Show(@"pathdefault = null, gw not installed?");
            }
        }

        openFileDialog.InitialDirectory = pathdefault;
        openFileDialog.Filter = "Guild Wars|Gw.exe";
        openFileDialog.RestoreDirectory = true;

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            textBoxPath.Text = openFileDialog.FileName;
        }
    }

    private void ButtonTogglePasswordVisibility_Click(object sender, EventArgs e)
    {
        textBoxPassword.PasswordChar = textBoxPassword.PasswordChar == '\0' ? '*' : '\0';
    }

    private void ButtonMods_Click(object sender, EventArgs e)
    {
        using var modForm = new ModManagerForm(account);
        modForm.ShowDialog();
    }
}
