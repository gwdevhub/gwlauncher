using GW_Launcher.Utilities;
using Microsoft.Win32;

namespace GW_Launcher.Forms;

public partial class CryptPassForm : Form
{
    private const string REGISTRY_KEY = @"SOFTWARE\GW_Launcher\Session";
    private const string PASSWORD_VALUE = "CachedPassword";

    private bool passwordSubmitted = false;

    public CryptPassForm()
    {
        PasswordHash = Array.Empty<byte>();
        InitializeComponent();
    }

    // SHA-256 of the master password the user typed.
    public byte[] PasswordHash { get; private set; }

    public static byte[]? GetCachedPasswordHash()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY);
            if (key?.GetValue(PASSWORD_VALUE) is string cachedHash)
            {
                var protectedHashBytes = Convert.FromBase64String(cachedHash);
                return ProtectedData.Unprotect(protectedHashBytes, null, DataProtectionScope.CurrentUser);
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

            var protectedHashBytes = ProtectedData.Protect(PasswordHash, null, DataProtectionScope.CurrentUser);
            var protectedHashBytesBase64 = Convert.ToBase64String(protectedHashBytes);

            key.SetValue(PASSWORD_VALUE, protectedHashBytesBase64, RegistryValueKind.String);
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

        PasswordHash = AccountManager.HashPassword(textBoxPassword.Text);
        textBoxPassword.Clear();

        // Store password hash in registry if checkbox is checked
        StoreCachedPassword();
        passwordSubmitted = true;
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

    private void CryptPassForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !passwordSubmitted)
        {
            this.DialogResult = DialogResult.Abort;
        }
    }
}
