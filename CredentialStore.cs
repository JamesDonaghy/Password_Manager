using System;
using System.IO;

namespace PasswordManager
{
    // Stores the local master-password credential in a file under the current user's
    // AppData folder. We keep it outside the project/repo on purpose so it never gets
    // committed by accident. Only the PBKDF2 hash from PasswordHasher is written here—
    // never the plaintext master password. A proper password hash is safe to persist
    // as-is because it isn't reversible.
    public static class CredentialStore
    {
        private static readonly string CredentialsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager",
            "credentials.dat");

        // Returns true when a master password has already been configured on this machine.
        public static bool CredentialExists()
        {
            return File.Exists(CredentialsFilePath);
        }

        // Writes the supplied password hash. Creates the PasswordManager folder under
        // AppData the first time a master password is set up.
        public static void SaveMasterPasswordHash(string passwordHash)
        {
            string directory = Path.GetDirectoryName(CredentialsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(CredentialsFilePath, passwordHash);
        }

        // Reads the previously stored password hash. Call only after CredentialExists()
        // has returned true.
        public static string LoadMasterPasswordHash()
        {
            return File.ReadAllText(CredentialsFilePath);
        }
    }
}