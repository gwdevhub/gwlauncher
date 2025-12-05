namespace GW_Launcher.Utilities;

public class Encryption
{
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

    // Legacy decryption method for backwards compatibility with old format
    public static string DecryptLegacy(byte[] textBytes, byte[] cryptPass)
    {
        // Legacy hardcoded IV from the original implementation
        var legacyIv = new byte[]
            { 0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85, 0xc8, 0x93, 0x48, 0x45, 0xcf, 0xa0, 0xfa, 0x85 };

        using var aes = Aes.Create();
        using var decrypt = aes.CreateDecryptor(cryptPass, legacyIv);

        var cryptBytes = decrypt.TransformFinalBlock(textBytes, 0, textBytes.Length);
        var rawJson = Encoding.UTF8.GetString(cryptBytes);

        if (!rawJson.StartsWith("SHIT"))
        {
            throw new Exception("Invalid legacy format");
        }

        return rawJson[4..]; // Remove "SHIT" prefix
    }

}
