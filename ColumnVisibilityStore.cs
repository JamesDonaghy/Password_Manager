using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PasswordManager
{
    /// Persists which grid columns the user has hidden via "Configure Columns...", so the
    /// choice survives restarting the app instead of resetting to all-columns-visible every
    /// launch. Stored as plain (unencrypted) JSON, same as UsernameSuggestionsStore - which
    /// columns are shown isn't sensitive data, so this doesn't need the vault's encryption.
    public static class ColumnVisibilityStore
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager",
            "hidden_columns.dat");

        /// Returns the underlying column names (Account property names) that should stay
        /// hidden, or an empty list if nothing's been saved yet (i.e. every column visible,
        /// the existing default).
        public static List<string> LoadHiddenColumns()
        {
            if (!File.Exists(FilePath))
            {
                return new List<string>();
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch (Exception)
            {
                // A corrupted/unreadable settings file shouldn't block the app from starting -
                // fall back to the same "everything visible" default as a first run.
                return new List<string>();
            }
        }

        public static void SaveHiddenColumns(IEnumerable<string> hiddenColumnNames)
        {
            string directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(new List<string>(hiddenColumnNames)));
        }
    }
}