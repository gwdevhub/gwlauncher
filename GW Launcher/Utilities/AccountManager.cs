using GW_Launcher.Forms;

namespace GW_Launcher.Utilities;

public class AccountManager : IEnumerable<Account>, IDisposable
{
    private readonly string _filePath = "Accounts.json";

    private List<Account> _accounts = new();
    private byte[]? _cryptPass;

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
        _cryptPass = null;

        filePath ??= _filePath;

        switch (Program.settings.Encrypt)
        {
            case false:
                try
                {
                    var text = File.ReadAllText(filePath);
                    _accounts = JsonConvert.DeserializeObject<List<Account>>(text) ?? _accounts;
                }
                catch (FileNotFoundException)
                {
                    // silent
                    File.WriteAllText(filePath, "[]");
                    _accounts.Clear();
                }

                break;
            case true:
            {
                // First try to get cached password
                _cryptPass = CryptPassForm.GetCachedPassword();

                // If no cached password, show the form
                if (_cryptPass == null)
                {
                    var form = new CryptPassForm();
                    form.ShowDialog();
                    _cryptPass = form.Password;
                }

                if (_cryptPass is { Length: > 0 })
                {
                    try
                    {
                        var textBytes = File.ReadAllBytes(filePath);
                        var password = Encoding.UTF8.GetString(_cryptPass);

                        string rawJson;

                        // First try new SecureAES method
                        try
                        {
                            rawJson = Encryption.SecureAES.Decrypt(textBytes, password);
                            _accounts = JsonConvert.DeserializeObject<List<Account>>(rawJson) ?? _accounts;
                        }
                        catch
                        {
                            // SecureAES failed, try legacy decryption method
                            try
                            {
                                rawJson = Encryption.DecryptLegacy(textBytes, _cryptPass);
                                // Legacy decryption succeeded, migrate to new encryption immediately by forcing a save
                                _accounts = JsonConvert.DeserializeObject<List<Account>>(rawJson) ?? _accounts;
                                Save(filePath); // This will save using the new SecureAES format
                            }
                            catch
                            {
                                // Both methods failed - wrong password
                                CryptPassForm.ClearCachedPassword();
                                MessageBox.Show("Incorrect password.\nRestart launcher and try again.",
                                    @"GW Launcher - Invalid Password");
                                throw new Exception("Wrong password");
                            }
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        // silent
                        var rawJson = "[]";
                        var password = Encoding.UTF8.GetString(_cryptPass);
                        var encrypted = Encryption.SecureAES.Encrypt(rawJson, password);
                        File.WriteAllBytes(filePath, encrypted);
                        _accounts.Clear();
                    }
                }

                break;
            }
        }

        foreach (var account in _accounts)
        {
            account.active = false;
            account.guid ??= Guid.NewGuid();
            account.mods ??= new List<Mod>();
        }
    }

    public void Save(string? filePath = null)
    {
        filePath ??= _filePath;

        var text = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
        if (!Program.settings.Encrypt)
        {
            File.WriteAllText(filePath, text);
        }
        else
        {
            Debug.Assert(_cryptPass != null, nameof(_cryptPass) + " != null");
            var password = Encoding.UTF8.GetString(_cryptPass);
            var encryptedBytes = Encryption.SecureAES.Encrypt(text, password);
            File.WriteAllBytes(filePath, encryptedBytes);
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
