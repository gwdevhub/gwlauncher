using GW_Launcher.Classes;

namespace GW_Launcher.Forms;

public partial class SettingsForm : Form
{
	private GlobalSettings _settings;

	public SettingsForm()
	{
		_settings = Program.settings;
		InitializeComponent();
		LoadSettings();
	}

	private void LoadSettings()
	{
		checkBoxEncrypt.Checked = _settings.Encrypt;
		checkBoxCheckForUpdates.Checked = _settings.CheckForUpdates;
		checkBoxAutoUpdate.Checked = _settings.AutoUpdate;
		checkBoxLaunchMinimized.Checked = _settings.LaunchMinimized;
		numericUpDownTimeout.Value = _settings.TimeoutOnModlaunch;

		// Auto-update should only be enabled if check for updates is enabled
		checkBoxAutoUpdate.Enabled = _settings.CheckForUpdates;
	}

	private void SaveSettings()
	{
		_settings.Encrypt = checkBoxEncrypt.Checked;
		_settings.CheckForUpdates = checkBoxCheckForUpdates.Checked;
		_settings.AutoUpdate = checkBoxAutoUpdate.Checked;
		_settings.LaunchMinimized = checkBoxLaunchMinimized.Checked;
		_settings.TimeoutOnModlaunch = (uint)numericUpDownTimeout.Value;

		Program.settings = _settings;
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

	private void CheckBoxEncrypt_CheckedChanged(object sender, EventArgs e)
	{
		if (checkBoxEncrypt.Checked != Program.settings.Encrypt)
		{
			MessageBox.Show(
				"Changing encryption settings will require restarting the application to take effect.",
				"Encryption Setting",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information);
		}
	}
}
