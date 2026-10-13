using GW_Launcher.Forms;

namespace GW_Launcher.Utilities;

public class AccountManager : IEnumerable<Account>, IDisposable
{
    private readonly string _filePath = "Accounts.json";

    private List<Account> _accounts = new();

    // SHA-256 of the master password protecting Accounts.json; null means unencrypted.
    private byte[]? _passwordHash;

    public bool IsEncrypted => _passwordHash != null;

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
        _passwordHash = null;
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
        if (!magicEncrypted && AccountFile.TryParse<Account>(raw, out var plainAccounts))
        {
            _accounts = plainAccounts;
            PostProcess();
            return;
        }

        var legacyEncrypted = !magicEncrypted;

        // Encrypted: get the master password from cache or by prompting.
        var passwordHash = CryptPassForm.GetCachedPasswordHash();
        if (passwordHash == null)
        {
            using var form = new CryptPassForm();
            if (form.ShowDialog() == DialogResult.Abort)
            {
                throw new OperationCanceledException();
            }
            passwordHash = form.PasswordHash;
        }

        try
        {
            var rawJson = AccountFile.Decrypt(raw, passwordHash, magicEncrypted);
            _accounts = JsonConvert.DeserializeObject<List<Account>>(rawJson) ?? _accounts;
            _passwordHash = passwordHash;

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
        if (_passwordHash == null)
        {
            File.WriteAllText(filePath, text);
        }
        else
        {
            File.WriteAllBytes(filePath, AccountFile.Encrypt(text, _passwordHash));
        }
    }

    // Re-save the account storage under a new master password. An empty password
    // removes encryption and writes plain JSON. Applies immediately, no restart.
    public void SetPassword(string newPassword)
    {
        _passwordHash = string.IsNullOrEmpty(newPassword) ? null : HashPassword(newPassword);
        // A stale cached password would fail to decrypt on next launch; drop it so the
        // user is re-prompted (and can opt back into caching) with the new password.
        CryptPassForm.ClearCachedPassword();
        Save(_filePath);
    }

    public bool IsCurrentPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return _passwordHash == null;
        }

        return _passwordHash != null &&
               CryptographicOperations.FixedTimeEquals(_passwordHash, HashPassword(password));
    }

    public static byte[] HashPassword(string password)
    {
        return AccountFile.HashPassword(password);
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
