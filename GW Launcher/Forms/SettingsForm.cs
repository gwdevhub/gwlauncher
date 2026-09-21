using GW_Launcher.Classes;

namespace GW_Launcher.Forms;

public partial class SettingsForm : Form
{
	private GlobalSettings _settings;

	public SettingsForm()
	{
		_settings = Program.Settings;
		InitializeComponent();
		LoadSettings();
	}

	private void LoadSettings()
	{
		textBoxPassword.PlaceholderText = Program.Accounts.IsEncrypted ? "(set)" : "(none)";
		checkBoxCheckForUpdates.Checked = _settings.CheckForUpdates;
		checkBoxAutoUpdate.Checked = _settings.AutoUpdate;
		checkBoxLaunchMinimized.Checked = _settings.LaunchMinimized;
		checkBoxKeepLauncherOpen.Checked = _settings.KeepLauncherOpen;
		numericUpDownTimeout.Value = _settings.TimeoutOnModlaunch;

		// Auto-update should only be enabled if check for updates is enabled
		checkBoxAutoUpdate.Enabled = _settings.CheckForUpdates;
	}

	private void SaveSettings()
	{
		_settings.CheckForUpdates = checkBoxCheckForUpdates.Checked;
		_settings.AutoUpdate = checkBoxAutoUpdate.Checked;
		_settings.LaunchMinimized = checkBoxLaunchMinimized.Checked;
		_settings.KeepLauncherOpen = checkBoxKeepLauncherOpen.Checked;
		_settings.TimeoutOnModlaunch = (uint)numericUpDownTimeout.Value;

		Program.Settings = _settings;
		_settings.Save();
	}

	private void ButtonOK_Click(object sender, EventArgs e)
	{
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

	private void ButtonApplyPassword_Click(object sender, EventArgs e)
	{
		var newPassword = textBoxPassword.Text;
		if (Program.Accounts.IsCurrentPassword(newPassword))
		{
			MessageBox.Show("The master password is unchanged.", "GW Launcher - Encryption",
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		// The field is no longer pre-filled, so an empty Apply could remove encryption by accident.
		if (newPassword.Length == 0 &&
			MessageBox.Show("Remove the master password and store accounts unencrypted?",
				"GW Launcher - Encryption", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
		{
			return;
		}

		try
		{
			Program.Accounts.SetPassword(newPassword);
			textBoxPassword.Clear();
			textBoxPassword.PlaceholderText = Program.Accounts.IsEncrypted ? "(set)" : "(none)";
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to re-save account storage:\n" + ex.Message,
				"GW Launcher - Encryption", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}

		MessageBox.Show(
			newPassword.Length == 0
				? "Account storage is now unencrypted."
				: "Account storage re-saved with the new master password.",
			"GW Launcher - Encryption", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void CheckBoxShowPassword_CheckedChanged(object sender, EventArgs e)
	{
		textBoxPassword.UseSystemPasswordChar = !checkBoxShowPassword.Checked;
	}
}
