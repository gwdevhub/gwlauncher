using GW_Launcher.Forms;

namespace GW_Launcher.Utilities;

public class AccountManager : IEnumerable<Account>, IDisposable
{
    private readonly SymmetricAlgorithm _crypt = Aes.Create();
    private readonly string _filePath = "Accounts.json";

    private readonly byte[] _salsaIv =
        { 0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85, 0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85 };

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
        _crypt.Dispose();
    }

    IEnumerator<Account> IEnumerable<Account>.GetEnumerator()
    {
        return ((IEnumerable<Account>)_accounts).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _accounts.GetEnumerator();
    }

    public void Load(string? filePath = null)
    {
        if (Program.settings.Encrypt)
        {
            var form = new CryptPassForm();
            form.ShowDialog();
            _cryptPass = form.Password;
        }

        filePath ??= _filePath;

        if (!Program.settings.Encrypt)
        {
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
        }
        else
        {
            Debug.Assert(_cryptPass != null, nameof(_cryptPass) + " != null");
            try
            {
                var textBytes = File.ReadAllBytes(filePath);
                using var decrypt = _crypt.CreateDecryptor(_cryptPass, _salsaIv);
                try
                {
                    var cryptBytes = decrypt.TransformFinalBlock(textBytes, 0, textBytes.Length);
                    var rawJson = Encoding.UTF8.GetString(cryptBytes);
                    if (!rawJson.StartsWith("SHIT"))
                    {
                        throw new Exception();
                    }

                    var text = rawJson[4..];
                    _accounts = JsonConvert.DeserializeObject<List<Account>>(text) ?? _accounts;
                }
                catch (Exception)
                {
                    MessageBox.Show("Incorrect password.\n Restart launcher and try again.",
                        @"GW Launcher - Invalid Password");
                    throw new Exception("Wrong password");
                }
            }
            catch (FileNotFoundException)
            {
                // silent
                var bytes = Encoding.UTF8.GetBytes("SHIT[]");
                using (var encrypt = _crypt.CreateEncryptor(_cryptPass, _salsaIv))
                {
                    var cryptBytes = encrypt.TransformFinalBlock(bytes, 0, bytes.Length);
                    File.WriteAllBytes(filePath, cryptBytes);
                }

                _accounts.Clear();
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
            text = "SHIT" + text;
            var bytes = Encoding.UTF8.GetBytes(text);
            Debug.Assert(_cryptPass != null, nameof(_cryptPass) + " != null");
            using var encrypt = _crypt.CreateEncryptor(_cryptPass, _salsaIv);
            var cryptBytes = encrypt.TransformFinalBlock(bytes, 0, bytes.Length);
            File.WriteAllBytes(filePath, cryptBytes);
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
