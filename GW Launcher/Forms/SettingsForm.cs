using GW_Launcher.Classes;

namespace GW_Launcher.Forms;

public partial class SettingsForm : Form
{
	private const int PasswordSectionHeight = 56;

	private GlobalSettings _settings;
	private bool _passwordSectionVisible = true;

	public SettingsForm()
	{
		_settings = Program.Settings;
		InitializeComponent();
		LoadSettings();
	}

	private void LoadSettings()
	{
		// Legacy encrypted files are detected and decrypted at load, so IsEncrypted is already accurate here.
		textBoxPassword.Text = Program.Accounts.CurrentPassword;
		checkBoxProtectAccounts.Checked = Program.Accounts.IsEncrypted;
		SetPasswordSectionVisible(checkBoxProtectAccounts.Checked);
		checkBoxCheckForUpdates.Checked = _settings.CheckForUpdates;
		checkBoxAutoUpdate.Checked = _settings.AutoUpdate;
		checkBoxLaunchMinimized.Checked = _settings.LaunchMinimized;
		checkBoxKeepInSystemTray.Checked = _settings.KeepInSystemTray;
		checkBoxLaunchMinimized.Enabled = checkBoxKeepInSystemTray.Checked;
		numericUpDownTimeout.Value = _settings.TimeoutOnModlaunch;

		// Auto-update should only be enabled if check for updates is enabled
		checkBoxAutoUpdate.Enabled = _settings.CheckForUpdates;
	}

	private void SaveSettings()
	{
		_settings.CheckForUpdates = checkBoxCheckForUpdates.Checked;
		_settings.AutoUpdate = checkBoxAutoUpdate.Checked;
		_settings.LaunchMinimized = checkBoxLaunchMinimized.Checked;
		_settings.KeepInSystemTray = checkBoxKeepInSystemTray.Checked;
		_settings.TimeoutOnModlaunch = (uint)numericUpDownTimeout.Value;

		Program.Settings = _settings;
		_settings.Save();
	}

	private void ButtonOK_Click(object sender, EventArgs e)
	{
		if (!ApplyPassword())
			return;

		SaveSettings();
		DialogResult = DialogResult.OK;
		Close();
	}

	private void ButtonCancel_Click(object sender, EventArgs e)
	{
		DialogResult = DialogResult.Cancel;
		Close();
	}

	private void CheckBoxCheckForUpdates_CheckedChanged(object sender, EventArgs e)
	{
		// Auto-update should only be available if check for updates is enabled
		checkBoxAutoUpdate.Enabled = checkBoxCheckForUpdates.Checked;
		if (!checkBoxCheckForUpdates.Checked)
		{
			checkBoxAutoUpdate.Checked = false;
		}
	}

	private void CheckBoxKeepInSystemTray_CheckedChanged(object sender, EventArgs e)
	{
		// Launching minimised means starting hidden in the tray, so it only applies in tray mode
		checkBoxLaunchMinimized.Enabled = checkBoxKeepInSystemTray.Checked;
	}

	private void CheckBoxProtectAccounts_CheckedChanged(object sender, EventArgs e)
	{
		SetPasswordSectionVisible(checkBoxProtectAccounts.Checked);
	}

	private void SetPasswordSectionVisible(bool visible)
	{
		if (visible == _passwordSectionVisible)
			return;

		_passwordSectionVisible = visible;
		labelPassword.Visible = textBoxPassword.Visible = checkBoxShowPassword.Visible = visible;

		var delta = visible ? PasswordSectionHeight : -PasswordSectionHeight;
		foreach (var control in new Control[]
		         {
			         checkBoxKeepInSystemTray, labelDescKeepInSystemTray, checkBoxLaunchMinimized, labelDescLaunchMinimized,
			         groupBoxUpdates, groupBoxAdvanced, buttonOK,
			         buttonCancel
		         })
		{
			control.Top += delta;
		}

		groupBoxGeneral.Height += delta;
		ClientSize = new Size(ClientSize.Width, ClientSize.Height + delta);
	}

	// Returns false if the user should stay on the form (invalid input, declined, or the re-save failed).
	private bool ApplyPassword()
	{
		var protect = checkBoxProtectAccounts.Checked;
		var newPassword = protect ? textBoxPassword.Text : "";

		if (protect && newPassword.Length == 0)
		{
			MessageBox.Show("Enter a password, or untick \"Password protect Accounts.json\".",
				"GW Launcher - Encryption", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			textBoxPassword.Focus();
			return false;
		}

		if (newPassword == Program.Accounts.CurrentPassword)
			return true;

		if (!protect && MessageBox.Show(
			    "Accounts.json will be stored as plain text, including your account passwords. Remove password protection?",
			    "GW Launcher - Encryption", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
		{
			return false;
		}

		try
		{
			Program.Accounts.SetPassword(newPassword);
			return true;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to re-save account storage:\n" + ex.Message,
				"GW Launcher - Encryption", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return false;
		}
	}

	private void CheckBoxShowPassword_CheckedChanged(object sender, EventArgs e)
	{
		textBoxPassword.UseSystemPasswordChar = !checkBoxShowPassword.Checked;
	}
}
