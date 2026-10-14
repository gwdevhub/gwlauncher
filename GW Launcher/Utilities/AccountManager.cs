namespace GW_Launcher.Utilities;

// Accounts.json for the Windows launcher. Opening an encrypted file needs the master password, which the host supplies.
public class AccountManager : IEnumerable<Account>
{
    private readonly string _filePath;
    private List<Account> _accounts = new();

    // SHA-256 of the master password protecting Accounts.json; null means unencrypted.
    private byte[]? _passwordHash;

    public AccountManager(string filePath)
    {
        _filePath = filePath;
    }

    public bool IsEncrypted => _passwordHash != null;

    public int Length => _accounts.Count;

    public Account this[int index]
    {
        get => _accounts[index];
        set
        {
            _accounts[index] = value;
            Save();
        }
    }

    IEnumerator<Account> IEnumerable<Account>.GetEnumerator() => _accounts.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _accounts.GetEnumerator();

    public int IndexOf(string accountName) => _accounts.FindIndex(account => account.Name == accountName);

    public int IndexOf(Account account) => _accounts.IndexOf(account);

    // Opens the file. Returns false when it's encrypted and the password hash doesn't open it.
    public bool Load(byte[]? passwordHash = null)
    {
        _passwordHash = null;

        if (!File.Exists(_filePath))
        {
            // Fresh install: start unencrypted with an empty account list.
            _accounts.Clear();
            File.WriteAllText(_filePath, "[]");
            PostProcess();
            return true;
        }

        var raw = File.ReadAllBytes(_filePath);
        var magicEncrypted = Encryption.IsEncrypted(raw);

        // Anything that parses as JSON is plain text; otherwise the file is encrypted and needs the password.
        if (!magicEncrypted && AccountFile.TryParse<Account>(raw, out var plainAccounts))
        {
            _accounts = plainAccounts;
            PostProcess();
            return true;
        }

        if (passwordHash == null)
        {
            return false;
        }

        try
        {
            _accounts = JsonConvert.DeserializeObject<List<Account>>(AccountFile.Decrypt(raw, passwordHash, magicEncrypted)) ?? _accounts;
        }
        catch (Exception ex) when (ex is JsonException or System.Security.Cryptography.CryptographicException or InvalidDataException)
        {
            return false;
        }

        _passwordHash = passwordHash;

        // Legacy ciphertext without the magic prefix is rewritten in the current format.
        if (!magicEncrypted)
        {
            Save();
        }

        PostProcess();
        return true;
    }

    // Re-reads the file under the password it was opened with. On failure the open state is kept, so a later save can't drop the encryption.
    public bool Reload()
    {
        var hash = _passwordHash;
        if (Load(hash))
        {
            return true;
        }

        _passwordHash = hash;
        return false;
    }

    public void Save()
    {
        var text = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
        if (_passwordHash == null)
        {
            File.WriteAllText(_filePath, text);
        }
        else
        {
            File.WriteAllBytes(_filePath, AccountFile.Encrypt(text, _passwordHash));
        }
    }

    // An empty password removes encryption and writes plain JSON. Applies immediately.
    public void SetPassword(string newPassword)
    {
        _passwordHash = string.IsNullOrEmpty(newPassword) ? null : HashPassword(newPassword);
        Save();
    }

    public bool IsCurrentPassword(string password) => AccountFile.IsCurrent(_passwordHash, password);

    public static byte[] HashPassword(string password) => AccountFile.HashPassword(password);

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
        Save();
    }

    public void Remove(int index)
    {
        _accounts.RemoveAt(index);
        Save();
    }

    public void Move(int oldIndex, int newIndex)
    {
        var account = _accounts[oldIndex];
        _accounts.RemoveAt(oldIndex);
        _accounts.Insert(newIndex, account);
        Save();
    }
}
