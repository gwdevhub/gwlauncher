namespace GW_Launcher.Utilities;

// The Accounts.json format shared by the Windows and Linux stores: plain JSON, or the "encrypted" magic prefix plus AES.
public static class AccountFile
{
    public static byte[] HashPassword(string password)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(password));
    }

    // The SecureAES password string is the UTF-8 view of SHA-256(plaintext), so existing encrypted files still decrypt.
    public static string DeriveKey(byte[] passwordHash)
    {
        return Encoding.UTF8.GetString(passwordHash);
    }

    // Returns false when the content isn't JSON, which means it is encrypted. An empty file is an empty list.
    public static bool TryParse<T>(byte[] raw, out List<T> items)
    {
        try
        {
            // Strip the BOM: Newtonsoft rejects U+FEFF, which would misclassify a hand-edited file as encrypted.
            var text = Encoding.UTF8.GetString(raw).TrimStart('﻿');
            items = JsonConvert.DeserializeObject<List<T>>(text) ?? new List<T>();
            return true;
        }
        catch (JsonException)
        {
            items = new List<T>();
            return false;
        }
    }

    // Legacy ciphertext without the magic prefix tries the current AES format first, then the original "SHIT" format.
    public static string Decrypt(byte[] raw, byte[] passwordHash, bool magicEncrypted)
    {
        var key = DeriveKey(passwordHash);
        if (magicEncrypted)
        {
            return Encryption.SecureAES.Decrypt(raw[Encryption.MagicPrefix.Length..], key);
        }

        try
        {
            return Encryption.SecureAES.Decrypt(raw, key);
        }
        catch
        {
            return Encryption.DecryptLegacy(raw, passwordHash);
        }
    }

    public static byte[] Encrypt(string json, byte[] passwordHash)
    {
        var encrypted = Encryption.SecureAES.Encrypt(json, DeriveKey(passwordHash));
        var output = new byte[Encryption.MagicPrefix.Length + encrypted.Length];
        Buffer.BlockCopy(Encryption.MagicPrefix, 0, output, 0, Encryption.MagicPrefix.Length);
        Buffer.BlockCopy(encrypted, 0, output, Encryption.MagicPrefix.Length, encrypted.Length);
        return output;
    }
}
