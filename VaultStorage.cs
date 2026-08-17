using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PasswordManager
{
    /// Encrypts and persists the account vault to a file in the user's AppData folder,
    /// using a key derived from the master password. Uses AES-GCM (authenticated
    /// encryption), so a corrupted or tampered file fails decryption loudly instead of
    /// silently returning garbage data.
    ///
    /// A fresh random salt (and therefore a fresh derived key) and nonce are generated on
    /// every save. This is deliberately simple: because the key itself changes every time,
    /// there is no risk of ever reusing the same (key, nonce) pair, which is the one hard
    /// requirement AES-GCM depends on for its security guarantees.
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

        // Written as a side effect of SaveVault's atomic File.Replace - see below.
        private static readonly string VaultBackupFilePath = VaultFilePath + ".bak";

        public static bool VaultExists()
        {
            return File.Exists(VaultFilePath);
        }

        /// Loads and decrypts the vault. Returns an empty list if no vault file exists yet
        /// (e.g. right after first setting up a master password).
        ///
        /// If the primary vault file can't be decrypted (corrupted, tampered with, or an
        /// interrupted write that somehow got past SaveVault's atomic swap), this falls
        /// back to the .bak file left behind by the previous save, rather than giving up
        /// and returning an empty vault straight away. loadedFromBackup tells the caller
        /// this happened, so the user can be told their data may be one save behind rather
        /// than silently trusting it's current.
        public static List<Account> LoadVault(string masterPassword, out bool loadedFromBackup)
        {
            loadedFromBackup = false;

            if (!VaultExists())
            {
                return new List<Account>();
            }

            try
            {
                return DecryptVaultFile(VaultFilePath, masterPassword);
            }
            catch (Exception primaryEx)
            {
                if (!File.Exists(VaultBackupFilePath))
                {
                    throw;
                }

                try
                {
                    List<Account> accounts = DecryptVaultFile(VaultBackupFilePath, masterPassword);
                    loadedFromBackup = true;
                    return accounts;
                }
                catch (Exception)
                {
                    // Backup didn't work either - surface the original failure, since that's
                    // the file the user actually expected to be loaded.
                    throw primaryEx;
                }
            }
        }

        private static List<Account> DecryptVaultFile(string filePath, string masterPassword)
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);

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

            // Throws CryptographicException if the key is wrong or the data has been
            // tampered with/corrupted - that's AES-GCM's authentication check failing,
            // which is exactly the "fail loudly" behavior we want here.
            using (var aesGcm = new AesGcm(key, TagSizeBytes))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            string json = Encoding.UTF8.GetString(plaintext);
            return JsonSerializer.Deserialize<List<Account>>(json) ?? new List<Account>();
        }

        /// Encrypts and saves the given account list, overwriting any previous vault file.
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

            // Write to a temporary file first, then atomically swap it into place, rather
            // than writing directly to VaultFilePath. Since the vault is authenticated
            // encryption, a file left half-written by a crash or power loss mid-write would
            // fail decryption entirely on next load - not just losing the change being
            // saved, but making every previously saved entry unreadable too. Writing to a
            // temp file first means an interruption can only ever leave incomplete *temp*
            // data behind; the real vault file is never touched until the write has fully
            // succeeded.
            string tempFilePath = VaultFilePath + ".tmp";
            File.WriteAllBytes(tempFilePath, output);

            if (File.Exists(VaultFilePath))
            {
                // File.Replace performs the swap as a single atomic filesystem operation,
                // taking a backup of the previous version along the way. LoadVault falls
                // back to this backup if the primary file ever fails to decrypt.
                File.Replace(tempFilePath, VaultFilePath, VaultBackupFilePath);
            }
            else
            {
                // First save ever - nothing to atomically replace yet.
                File.Move(tempFilePath, VaultFilePath);
            }
        }

        private static byte[] DeriveKey(string masterPassword, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(masterPassword, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        }
    }
}