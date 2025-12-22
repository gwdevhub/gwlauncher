using Microsoft.Win32;

namespace GW_Launcher.Forms;

public partial class AddAccountForm : Form
{
	public Account account;

	public AddAccountForm()
	{
		account = new Account();
		InitializeComponent();
		LoadSteamLogo();
	}

	private void LoadSteamLogo()
	{
		pictureBoxSteamLogo.Image = Properties.Resources.steam;
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		SaveAccount();
		base.OnFormClosing(e);
	}
	private void ButtonDone_Click(object sender, EventArgs e)
	{
		SaveAccount();
	}
	private void SaveAccount()
	{
		account.title = textBoxTitle.Text;
		account.is_steam_account = checkBoxSteamAccount.Checked;
		account.email = textBoxEmail.Text;
		account.password = textBoxPassword.Text;
		account.character = textBoxCharacter.Text;
		account.gwpath = textBoxPath.Text;
		account.elevated = checkBoxElevated.Checked;
		account.extraargs = textBoxExtraArguments.Text;
		account.usePluginFolderMods = checkBoxUsePluginFolderMods.Checked;
		MainForm.OnAccountSaved(account);
	}

	private void AddAccountForm_Load(object sender, EventArgs e)
	{
		textBoxTitle.Text = account.title;
		checkBoxSteamAccount.Checked = account.is_steam_account;
		textBoxEmail.Text = account.email;
		textBoxPassword.Text = account.password;
		textBoxCharacter.Text = account.character;
		textBoxPath.Text = account.gwpath;
		checkBoxElevated.Checked = account.elevated;
		checkBoxUsePluginFolderMods.Checked = account.usePluginFolderMods;
		textBoxExtraArguments.Text = account.extraargs;
		UpdateCredentialFieldsState();
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

	private void CheckBoxSteamAccount_CheckedChanged(object sender, EventArgs e)
	{
		UpdateCredentialFieldsState();
	}

	private void UpdateCredentialFieldsState()
	{
		bool isSteam = checkBoxSteamAccount.Checked;

		// Hide credential fields for Steam accounts
		labelEmail.Visible = !isSteam;
		textBoxEmail.Visible = !isSteam;
		labelPassword.Visible = !isSteam;
		textBoxPassword.Visible = !isSteam;
		labelCharacter.Visible = !isSteam;
		textBoxCharacter.Visible = !isSteam;
		buttonTogglePasswordVisibility.Visible = !isSteam;

		// Show Steam logo when Steam account is selected
		pictureBoxSteamLogo.Visible = isSteam;
	}
}
