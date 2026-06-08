using GW_Launcher.Forms;

namespace GW_Launcher.Utilities;

public class AccountManager : IEnumerable<Account>, IDisposable
{
    private readonly string _filePath = "Accounts.json";

    private List<Account> _accounts = new();

    // The master password currently protecting Accounts.json, in plaintext.
    // Empty means the file is stored unencrypted.
    private string _password = "";

    // Whether the account storage is currently encrypted (a master password is set).
    public bool IsEncrypted => _password.Length > 0;

    // The current master password, used to pre-fill the settings field.
    public string CurrentPassword => _password;

    public AccountManager(string? filePath = null)
    {
        if (filePath == null)
        {
            return;
        }

        _filePath = filePath;
        Load(filePath);
    }

    public int Length => _accounts.Count;

    public Account this[int index]
    {
        get => _accounts[index];
        set
        {
            _accounts[index] = value;
            Save(_filePath);
        }
    }

    public Account? this[string email]
    {
        get => _accounts.Find(account => account.email == email);
        set
        {
            var index = _accounts.FindIndex(account => account.email == email);
            if (index != -1 && value != null)
            {
                this[index] = value;
            }
        }
    }

    public Account? this[Guid? guid]
    {
        get => _accounts.Find(account => account.guid == guid);
        set
        {
            var index = _accounts.FindIndex(account => account.guid == guid);
            if (index != -1 && value != null)
            {
                this[index] = value;
            }
        }
    }

    void IDisposable.Dispose()
    {
        GC.SuppressFinalize(this);
    }

    IEnumerator<Account> IEnumerable<Account>.GetEnumerator()
    {
        return ((IEnumerable<Account>)_accounts).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _accounts.GetEnumerator();
    }

    public int IndexOf(string accountName)
    {
        for (var i = 0; i < Length; i++)
        {
            if (this[i].Name == accountName)
            {
                return i;
            }
        }

        return -1;
    }
	public int IndexOf(Account account)
	{
		for (var i = 0; i < Length; i++)
		{
			if (this[i] == account)
			{
				return i;
			}
		}

		return -1;
	}

	public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= Length || newIndex < 0 || newIndex >= Length)
        {
            throw new ArgumentOutOfRangeException();
        }

        var account = _accounts[oldIndex];
        _accounts.RemoveAt(oldIndex);
        _accounts.Insert(newIndex, account);
        Save(_filePath);
    }

    public void Load(string? filePath = null)
    {
        _password = "";
        filePath ??= _filePath;

        if (!File.Exists(filePath))
        {
            // Fresh install: start unencrypted with an empty account list.
            _accounts.Clear();
            File.WriteAllText(filePath, "[]");
            PostProcess();
            return;
        }

        var raw = File.ReadAllBytes(filePath);

        var magicEncrypted = Encryption.IsEncrypted(raw);

        // Without a magic marker the file is either plain JSON or pre-magic ciphertext.
        // Rather than guessing from the leading bytes, just try to parse it as JSON: if it
        // parses it's plain text, and if it doesn't it's encrypted and needs a password.
        // This keeps legacy ciphertext reachable no matter what bytes it happens to start with.
        if (!magicEncrypted && TryParseAccounts(raw, out var plainAccounts))
        {
            _accounts = plainAccounts;
            PostProcess();
            return;
        }

        var legacyEncrypted = !magicEncrypted;

        // Encrypted: get the master password from cache or by prompting.
        var password = CryptPassForm.GetCachedPassword();
        if (password == null)
        {
            using var form = new CryptPassForm();
            form.ShowDialog();
            password = form.PasswordText;
        }

        try
        {
            var rawJson = Decrypt(raw, password, magicEncrypted);
            _accounts = JsonConvert.DeserializeObject<List<Account>>(rawJson) ?? _accounts;
            _password = password;

            // Migrate legacy ciphertext to the current magic-prefixed format.
            if (legacyEncrypted)
            {
                Save(filePath);
            }
        }
        catch
        {
            CryptPassForm.ClearCachedPassword();
            MessageBox.Show("Incorrect password.\nRestart launcher and try again.",
                @"GW Launcher - Invalid Password");
            throw new OperationCanceledException("Wrong password");
        }

        PostProcess();
    }

    public void Save(string? filePath = null)
    {
        filePath ??= _filePath;

        var text = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
        if (_password.Length == 0)
        {
            File.WriteAllText(filePath, text);
        }
        else
        {
            var encrypted = Encryption.SecureAES.Encrypt(text, DeriveKey(_password));
            var output = new byte[Encryption.MagicPrefix.Length + encrypted.Length];
            Buffer.BlockCopy(Encryption.MagicPrefix, 0, output, 0, Encryption.MagicPrefix.Length);
            Buffer.BlockCopy(encrypted, 0, output, Encryption.MagicPrefix.Length, encrypted.Length);
            File.WriteAllBytes(filePath, output);
        }
    }

    // Re-save the account storage under a new master password. An empty password
    // removes encryption and writes plain JSON. Applies immediately, no restart.
    public void SetPassword(string newPassword)
    {
        _password = newPassword ?? "";
        // A stale cached password would fail to decrypt on next launch; drop it so the
        // user is re-prompted (and can opt back into caching) with the new password.
        CryptPassForm.ClearCachedPassword();
        Save(_filePath);
    }

    private static string Decrypt(byte[] raw, string password, bool magicEncrypted)
    {
        var key = DeriveKey(password);
        if (magicEncrypted)
        {
            var payload = raw[Encryption.MagicPrefix.Length..];
            return Encryption.SecureAES.Decrypt(payload, key);
        }

        // Legacy ciphertext without a magic prefix: current AES format first, then the
        // original "SHIT"-prefixed format.
        try
        {
            return Encryption.SecureAES.Decrypt(raw, key);
        }
        catch
        {
            return Encryption.DecryptLegacy(raw, SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        }
    }

    // The SecureAES password string is the UTF-8 view of SHA-256(plaintext). This matches
    // how earlier versions derived it, so existing encrypted files still decrypt.
    private static string DeriveKey(string password)
    {
        return Encoding.UTF8.GetString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
    }

    // Try to read the raw bytes as a plain-text JSON account list. Returns false when the
    // content isn't valid JSON (e.g. encrypted ciphertext). An empty or whitespace file is
    // treated as a valid, empty list.
    private static bool TryParseAccounts(byte[] raw, out List<Account> accounts)
    {
        try
        {
            accounts = JsonConvert.DeserializeObject<List<Account>>(Encoding.UTF8.GetString(raw)) ?? new List<Account>();
            return true;
        }
        catch
        {
            accounts = new List<Account>();
            return false;
        }
    }

    private void PostProcess()
    {
        foreach (var account in _accounts)
        {
            account.active = false;
            account.guid ??= Guid.NewGuid();
            account.mods ??= new List<Mod>();
        }
    }

    public void Add(Account account)
    {
        _accounts.Add(account);
        Save(_filePath);
    }

    public void Remove(int index)
    {
        _accounts.RemoveAt(index);
        Save(_filePath);
    }

    public void Remove(string email)
    {
        _accounts.RemoveAll(account => account.email == email);
    }
}
