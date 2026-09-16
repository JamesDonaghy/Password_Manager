using System;
using System.IO;
using System.Text.Json;

namespace PasswordManager
{
    /// <summary>
    /// Simple app-wide preferences stored as plain JSON under AppData (not inside the
    /// encrypted vault). Defaults match the previous behaviour when no file exists yet.
    /// </summary>
    public static class AppPreferences
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PasswordManager",
            "preferences.json");

        private static bool loaded;
        private static bool showWebsiteIcons = true;
        private static AccountSortMode sortMode = AccountSortMode.NameAscending;

        /// When true, entry badges use website favicons when available; when false, the
        /// coloured letter badges are used exclusively (as before favicons were added).
        public static bool ShowWebsiteIcons
        {
            get
            {
                EnsureLoaded();
                return showWebsiteIcons;
            }
            set
            {
                EnsureLoaded();
                if (showWebsiteIcons == value)
                {
                    return;
                }

                showWebsiteIcons = value;
                Save();
            }
        }

        /// Display order for the vault list. Does not change stored vault order.
        public static AccountSortMode SortMode
        {
            get
            {
                EnsureLoaded();
                return sortMode;
            }
            set
            {
                EnsureLoaded();
                if (sortMode == value)
                {
                    return;
                }

                sortMode = value;
                Save();
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                string json = File.ReadAllText(FilePath);
                var data = JsonSerializer.Deserialize<PreferencesData>(json);
                if (data != null)
                {
                    showWebsiteIcons = data.ShowWebsiteIcons;
                    if (!string.IsNullOrEmpty(data.SortMode) &&
                        Enum.TryParse(data.SortMode, ignoreCase: true, out AccountSortMode parsed))
                    {
                        sortMode = parsed;
                    }
                }
            }
            catch
            {
                // Corrupt/unreadable file: keep defaults.
            }
        }

        private static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new PreferencesData
                {
                    ShowWebsiteIcons = showWebsiteIcons,
                    SortMode = sortMode.ToString()
                };
                File.WriteAllText(FilePath, JsonSerializer.Serialize(data));
            }
            catch
            {
                // Preference is still applied in-memory for this session.
            }
        }

        private sealed class PreferencesData
        {
            public bool ShowWebsiteIcons { get; set; } = true;
            public string SortMode { get; set; } = nameof(AccountSortMode.NameAscending);
        }
    }
}
