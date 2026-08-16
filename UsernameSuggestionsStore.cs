using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PasswordManager
{
    /// Persists a small, user-curated list of usernames/emails that show up as
    /// autocomplete suggestions in AddEntryForm, independent of what's actually been
    /// saved to the vault. Stored as plain (unencrypted) JSON - usernames/emails aren't
    /// sensitive in the way passwords are, so this doesn't need the vault's encryption.
    public static class UsernameSuggestionsStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager",
            "suggested_usernames.dat");

        public static List<string> LoadUsernames()
        {
            if (!File.Exists(FilePath))
            {
                return new List<string>();
            }

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }

        public static void SaveUsernames(List<string> usernames)
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(usernames));
        }
    }
}