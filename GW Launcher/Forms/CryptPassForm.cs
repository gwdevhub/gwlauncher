using Microsoft.Win32;

namespace GW_Launcher.Forms;

public partial class CryptPassForm : Form
{
	private const string REGISTRY_KEY = @"SOFTWARE\GW_Launcher\Session";
	private const string PASSWORD_VALUE = "CachedPassword";

	public CryptPassForm()
	{
		Password = Array.Empty<byte>();
		InitializeComponent();
	}

	public byte[] Password { get; private set; }

	public static byte[]? GetCachedPassword()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
			if (key?.GetValue(PASSWORD_VALUE) is byte[] cachedPassword)
			{
				return cachedPassword;
			}
		}
		catch
		{
			// Ignore registry access errors
		}
		return null;
	}

	public static void ClearCachedPassword()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY, true);
			key?.DeleteValue(PASSWORD_VALUE, false);
		}
		catch
		{
			// Ignore registry access errors
		}
	}

	private void StoreCachedPassword()
	{
		if (!checkBoxDontAsk.Checked) return;

		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY);
			key.SetValue(PASSWORD_VALUE, Password, RegistryValueKind.Binary);
		}
		catch
		{
			// Ignore registry access errors
		}
	}

	private void Finish()
	{
		if (textBoxPassword.Text == "")
		{
			return;
		}

		var sha = SHA256.Create();
		Password = sha.ComputeHash(Encoding.UTF8.GetBytes(textBoxPassword.Text));

		// Store password in registry if checkbox is checked
		StoreCachedPassword();

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
