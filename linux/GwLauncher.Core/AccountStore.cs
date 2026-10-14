using GW_Launcher.Utilities;

namespace GwLauncher.Linux;

// Reads and writes Accounts.json in the same plain or encrypted format as the WinForms launcher.
// Linux paths live in a sidecar beside it, so the Windows file's contents are never changed.
public sealed class AccountStore
{
    private readonly string _path;
    private readonly string _sidecarPath;
    private byte[]? _passwordHash;

    public AccountStore(string path)
    {
        _path = path;
        _sidecarPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "paths.json");
    }

    public List<LinuxAccount> Accounts { get; private set; } = new();

    public bool IsEncrypted => _passwordHash != null;

    public void Load(Func<string> promptPassword)
    {
        LoadAccounts(promptPassword);
        ApplySidecar();
    }

    public void Save()
    {
        WriteAccounts();
        WriteSidecar();
    }

    public void Add(LinuxAccount account)
    {
        Accounts.Add(account);
    }

    public void Replace(int index, LinuxAccount account)
    {
        Accounts[index] = account;
    }

    public void Remove(int index)
    {
        Accounts.RemoveAt(index);
    }

    public void Move(int from, int to)
    {
        var account = Accounts[from];
        Accounts.RemoveAt(from);
        Accounts.Insert(to, account);
    }

    // An empty password removes encryption and writes plain JSON.
    public void SetPassword(string password)
    {
        _passwordHash = string.IsNullOrEmpty(password) ? null : HashPassword(password);
        Save();
    }

    public bool IsCurrentPassword(string password) => AccountFile.IsCurrent(_passwordHash, password);

    public static byte[] HashPassword(string password)
    {
        return AccountFile.HashPassword(password);
    }

    private void LoadAccounts(Func<string> promptPassword)
    {
        _passwordHash = null;

        if (!File.Exists(_path))
        {
            Accounts = new List<LinuxAccount>();
            return;
        }

        var raw = File.ReadAllBytes(_path);
        var magicEncrypted = Encryption.IsEncrypted(raw);

        // Mirrors the WinForms loader: anything that parses as JSON is plain text, otherwise it needs a password.
        if (!magicEncrypted && AccountFile.TryParse<LinuxAccount>(raw, out var plain))
        {
            Accounts = plain;
            return;
        }

        var hash = AccountStore.HashPassword(promptPassword());
        try
        {
            var json = AccountFile.Decrypt(raw, hash, magicEncrypted);
            Accounts = JsonConvert.DeserializeObject<List<LinuxAccount>>(json) ?? new List<LinuxAccount>();
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // A wrong password usually fails decryption, but can also decrypt to bytes that aren't JSON.
            throw new InvalidOperationException("Incorrect master password.", ex);
        }

        _passwordHash = hash;
    }

    private void WriteAccounts()
    {
        var text = JsonConvert.SerializeObject(Accounts, Formatting.Indented);

        if (_passwordHash == null)
        {
            File.WriteAllText(_path, text);
            return;
        }

        File.WriteAllBytes(_path, AccountFile.Encrypt(text, _passwordHash));
    }

    private void ApplySidecar()
    {
        var sidecar = PathSidecar.Read(_sidecarPath);

        foreach (var account in Accounts)
        {
            if (!sidecar.Accounts.TryGetValue(account.GwPath, out var paths))
            {
                continue;
            }

            account.LinuxGwPath = paths.LinuxGwPath;
            foreach (var mod in account.Mods)
            {
                if (paths.Mods.TryGetValue(mod.FileName, out var linuxPath))
                {
                    mod.LinuxPath = linuxPath;
                }
            }
        }
    }

    // Rebuilt from the accounts being saved, so entries for removed accounts disappear.
    private void WriteSidecar()
    {
        var sidecar = new PathSidecar();

        foreach (var account in Accounts)
        {
            var mods = new Dictionary<string, string>();
            foreach (var mod in account.Mods.Where(mod => !string.IsNullOrEmpty(mod.LinuxPath)))
            {
                mods[mod.FileName] = mod.LinuxPath!;
            }

            if (string.IsNullOrEmpty(account.LinuxGwPath) && mods.Count == 0)
            {
                continue;
            }

            sidecar.Accounts[account.GwPath] = new PathSidecar.AccountPaths
            {
                LinuxGwPath = account.LinuxGwPath,
                Mods = mods
            };
        }

        sidecar.Write(_sidecarPath);
    }
}
