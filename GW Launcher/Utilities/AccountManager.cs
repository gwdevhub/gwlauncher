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

    public int IndexOf(string account_name)
    {
        for (var i = 0; i < Length; i++)
        {
            if (this[i].Name == account_name)
            {
                return i;
            }
        }
        return -1;
    }

    // Class to handle AES encryption and decryption with PBKDF2 key derivation, utilizing a random salt and IV for each encryption operation
    public static class SecureAES
    {
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 600_000; // Number of iterations for PBKDF2-HMAC-SHA256. 600k per OWASP standards, but can be drastically adjusted based on performance needs

        // Encrypt using AES with a password-based key derivation function (PBKDF2), embedding a random salt and IV to the ciphertext output
        public static byte[] Encrypt(string plainText, string password)
        {
            byte[] salt = GenerateRandomBytes(SaltSize);
            byte[] iv = GenerateRandomBytes(IvSize);
            byte[] key = DeriveKey(password, salt);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using var writer = new StreamWriter(cs, Encoding.UTF8);
            writer.Write(plainText);
            writer.Close();

            byte[] ciphertext = ms.ToArray();

            byte[] result = new byte[SaltSize + IvSize + ciphertext.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(iv, 0, result, SaltSize, IvSize);
            Buffer.BlockCopy(ciphertext, 0, result, SaltSize + IvSize, ciphertext.Length);

            return result;
        }

        // Decrypt by reading the prepended Salt and IV from the ciphertext, then using PBKDF2 to derive the key
        public static string Decrypt(byte[] encryptedData, string password)
        {
            if (encryptedData.Length < SaltSize + IvSize)
                throw new ArgumentException("Encrypted data too short to contain salt and IV.");

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            byte[] ciphertext = new byte[encryptedData.Length - SaltSize - IvSize];

            Buffer.BlockCopy(encryptedData, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(encryptedData, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(encryptedData, SaltSize + IvSize, ciphertext, 0, ciphertext.Length);

            byte[] key = DeriveKey(password, salt);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var ms = new MemoryStream(ciphertext);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        // Derive key using PBKDF2 with the specified password, salt and iterations
        private static byte[] DeriveKey(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(KeySize);
        }

        // Generate a random byte array of the specified length to use as salt or IV
        private static byte[] GenerateRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return bytes;
        }
    }

    public void Load(string? filePath = null)
    {
        if (Program.settings.Encrypt)
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
                var password = Encoding.UTF8.GetString(_cryptPass);
                var rawJson = SecureAES.Decrypt(textBytes, password);
                _accounts = JsonConvert.DeserializeObject<List<Account>>(rawJson) ?? _accounts;
            }
            catch (FileNotFoundException)
            {
                // silent
                var rawJson = "[]";
                var password = Encoding.UTF8.GetString(_cryptPass);
                var encrypted = SecureAES.Encrypt(rawJson, password);
                File.WriteAllBytes(filePath, encrypted);
                _accounts.Clear();
            }
            catch (Exception)
            {
                // Clear cached password on wrong password
                CryptPassForm.ClearCachedPassword();

                MessageBox.Show("Incorrect password.\nRestart launcher and try again.",
                    @"GW Launcher - Invalid Password");
                throw new Exception("Wrong password");
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
            var encryptedBytes = SecureAES.Encrypt(text, password);
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
