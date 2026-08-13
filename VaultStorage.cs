using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PasswordManager
{
    // Encrypts and persists the account vault to a file in the user's AppData folder,
    // using a key derived from the master password. Uses AES-GCM (authenticated
    // encryption), so a corrupted or tampered file fails decryption loudly instead of
    // silently returning garbage data.
    //
    // A fresh random salt (and therefore a fresh derived key) and nonce are generated on
    // every save. This is deliberately simple: because the key itself changes every time,
    // there is no risk of ever reusing the same (key, nonce) pair, which is the one hard
    // requirement AES-GCM depends on for its security guarantees.
    public static class VaultStorage
    {
        private const int SaltSizeBytes = 16;   // 128-bit salt for key derivation
        private const int KeySizeBytes = 32;    // 256-bit AES key
        private const int NonceSizeBytes = 12;  // 96-bit nonce, standard for AES-GCM
        private const int TagSizeBytes = 16;    // 128-bit authentication tag
        private const int Iterations = 210_000; // Independent of PasswordHasher's own salt/iterations

        private static readonly string VaultFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager",
            "vault.dat");

        public static bool VaultExists()
        {
            return File.Exists(VaultFilePath);
        }

        // Loads and decrypts the vault. Returns an empty list if no vault file exists yet
        // (e.g. right after first setting up a master password).
        public static List<Account> LoadVault(string masterPassword)
        {
            if (!VaultExists())
            {
                return new List<Account>();
            }

            byte[] fileBytes = File.ReadAllBytes(VaultFilePath);

            int headerLength = SaltSizeBytes + NonceSizeBytes + TagSizeBytes;
            if (fileBytes.Length <= headerLength)
            {
                throw new InvalidDataException("The vault file is too short to be valid.");
            }

            byte[] salt = fileBytes[0..SaltSizeBytes];
            byte[] nonce = fileBytes[SaltSizeBytes..(SaltSizeBytes + NonceSizeBytes)];
            byte[] tag = fileBytes[(SaltSizeBytes + NonceSizeBytes)..headerLength];
            byte[] ciphertext = fileBytes[headerLength..];

            byte[] key = DeriveKey(masterPassword, salt);
            byte[] plaintext = new byte[ciphertext.Length];

            // Throws CryptographicException if the key is wrong or the data has been tampered with or corrupted.
            using (var aesGcm = new AesGcm(key, TagSizeBytes))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            string json = Encoding.UTF8.GetString(plaintext);
            return JsonSerializer.Deserialize<List<Account>>(json) ?? new List<Account>();
        }

        // Encrypts and saves the given account list, overwriting any previous vault file.
        public static void SaveVault(List<Account> accounts, string masterPassword)
        {
            string directory = Path.GetDirectoryName(VaultFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] key = DeriveKey(masterPassword, salt);

            string json = JsonSerializer.Serialize(accounts);
            byte[] plaintext = Encoding.UTF8.GetBytes(json);

            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSizeBytes];

            using (var aesGcm = new AesGcm(key, TagSizeBytes))
            {
                aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            // Layout: [salt][nonce][tag][ciphertext] - salt and nonce aren't secret and
            // must be stored alongside the ciphertext so the file can be decrypted later.
            byte[] output = new byte[SaltSizeBytes + NonceSizeBytes + TagSizeBytes + ciphertext.Length];
            Buffer.BlockCopy(salt, 0, output, 0, SaltSizeBytes);
            Buffer.BlockCopy(nonce, 0, output, SaltSizeBytes, NonceSizeBytes);
            Buffer.BlockCopy(tag, 0, output, SaltSizeBytes + NonceSizeBytes, TagSizeBytes);
            Buffer.BlockCopy(ciphertext, 0, output, SaltSizeBytes + NonceSizeBytes + TagSizeBytes, ciphertext.Length);

            File.WriteAllBytes(VaultFilePath, output);
        }

        // Derives a key from the master password and salt using PBKDF2.
        private static byte[] DeriveKey(string masterPassword, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        }
    }
}