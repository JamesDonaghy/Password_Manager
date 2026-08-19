using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// Adds the given username if it isn't already in the list (case-insensitive match).
        /// Blank/whitespace usernames are ignored. This never removes anything - deleting a
        /// suggestion is always a manual action via ManageUsernamesForm, even if the account
        /// it came from is later edited or deleted.
        public static void UpsertUsername(string username)
        {
            UpsertUsernames(new[] { username });
        }

        /// Same as UpsertUsername, but for many usernames at once - reads and writes the
        /// file once regardless of how many are added, rather than once per username. Used
        /// to backfill every username already in the vault the first time this runs.
        public static void UpsertUsernames(IEnumerable<string> usernamesToAdd)
        {
            List<string> existing = LoadUsernames();
            var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

            bool changed = false;
            foreach (string username in usernamesToAdd)
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    continue;
                }

                string trimmed = username.Trim();
                if (existingSet.Add(trimmed))
                {
                    existing.Add(trimmed);
                    changed = true;
                }
            }

            if (changed)
            {
                SaveUsernames(existing);
            }
        }
    }
}