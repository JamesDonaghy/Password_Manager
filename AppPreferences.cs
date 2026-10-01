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
        private static int autoLockMinutes = 5;

        // Generation defaults used by Add/Edit Entry (and kept for future Generator reuse).
        private static int passwordLength = 20;
        private static bool passwordUppercase = true;
        private static bool passwordLowercase = true;
        private static bool passwordDigits = true;
        private static bool passwordSymbols = true;
        private static int passphraseWordCount = 4;
        private static bool passphraseCapitalize = true;
        private static bool passphraseNumbers = true;
        private static bool passphraseSpecial = true;

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

        /// Minutes of inactivity before the vault auto-locks. 0 means auto-lock is off.
        /// Default is 5 to match the previous fixed behaviour.
        public static int AutoLockMinutes
        {
            get
            {
                EnsureLoaded();
                return autoLockMinutes;
            }
            set
            {
                EnsureLoaded();
                int clamped = value < 0 ? 0 : value;
                if (autoLockMinutes == clamped)
                {
                    return;
                }

                autoLockMinutes = clamped;
                Save();
            }
        }

        public static int PasswordLength
        {
            get { EnsureLoaded(); return passwordLength; }
        }

        public static bool PasswordUppercase
        {
            get { EnsureLoaded(); return passwordUppercase; }
        }

        public static bool PasswordLowercase
        {
            get { EnsureLoaded(); return passwordLowercase; }
        }

        public static bool PasswordDigits
        {
            get { EnsureLoaded(); return passwordDigits; }
        }

        public static bool PasswordSymbols
        {
            get { EnsureLoaded(); return passwordSymbols; }
        }

        public static int PassphraseWordCount
        {
            get { EnsureLoaded(); return passphraseWordCount; }
        }

        public static bool PassphraseCapitalize
        {
            get { EnsureLoaded(); return passphraseCapitalize; }
        }

        public static bool PassphraseNumbers
        {
            get { EnsureLoaded(); return passphraseNumbers; }
        }

        public static bool PassphraseSpecial
        {
            get { EnsureLoaded(); return passphraseSpecial; }
        }

        /// Persists the current generation options from the entry form settings panel.
        public static void SaveGenerationOptions(
            int length,
            bool uppercase,
            bool lowercase,
            bool digits,
            bool symbols,
            int wordCount,
            bool capitalizeWords,
            bool includeNumbers,
            bool includeSpecial)
        {
            EnsureLoaded();
            passwordLength = Math.Clamp(length, 4, 64);
            passwordUppercase = uppercase;
            passwordLowercase = lowercase;
            passwordDigits = digits;
            passwordSymbols = symbols;
            // Keep at least one charset on in storage.
            if (!passwordUppercase && !passwordLowercase && !passwordDigits && !passwordSymbols)
            {
                passwordLowercase = true;
            }

            passphraseWordCount = Math.Clamp(wordCount, 3, 10);
            passphraseCapitalize = capitalizeWords;
            passphraseNumbers = includeNumbers;
            passphraseSpecial = includeSpecial;
            Save();
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

                    if (data.AutoLockMinutes.HasValue && data.AutoLockMinutes.Value >= 0)
                    {
                        autoLockMinutes = data.AutoLockMinutes.Value;
                    }

                    if (data.PasswordLength.HasValue)
                    {
                        passwordLength = Math.Clamp(data.PasswordLength.Value, 4, 64);
                    }

                    if (data.PasswordUppercase.HasValue) passwordUppercase = data.PasswordUppercase.Value;
                    if (data.PasswordLowercase.HasValue) passwordLowercase = data.PasswordLowercase.Value;
                    if (data.PasswordDigits.HasValue) passwordDigits = data.PasswordDigits.Value;
                    if (data.PasswordSymbols.HasValue) passwordSymbols = data.PasswordSymbols.Value;
                    if (data.PassphraseWordCount.HasValue)
                    {
                        passphraseWordCount = Math.Clamp(data.PassphraseWordCount.Value, 3, 10);
                    }

                    if (data.PassphraseCapitalize.HasValue) passphraseCapitalize = data.PassphraseCapitalize.Value;
                    if (data.PassphraseNumbers.HasValue) passphraseNumbers = data.PassphraseNumbers.Value;
                    if (data.PassphraseSpecial.HasValue) passphraseSpecial = data.PassphraseSpecial.Value;
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
                    SortMode = sortMode.ToString(),
                    AutoLockMinutes = autoLockMinutes,
                    PasswordLength = passwordLength,
                    PasswordUppercase = passwordUppercase,
                    PasswordLowercase = passwordLowercase,
                    PasswordDigits = passwordDigits,
                    PasswordSymbols = passwordSymbols,
                    PassphraseWordCount = passphraseWordCount,
                    PassphraseCapitalize = passphraseCapitalize,
                    PassphraseNumbers = passphraseNumbers,
                    PassphraseSpecial = passphraseSpecial
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
            public int? AutoLockMinutes { get; set; }
            public int? PasswordLength { get; set; }
            public bool? PasswordUppercase { get; set; }
            public bool? PasswordLowercase { get; set; }
            public bool? PasswordDigits { get; set; }
            public bool? PasswordSymbols { get; set; }
            public int? PassphraseWordCount { get; set; }
            public bool? PassphraseCapitalize { get; set; }
            public bool? PassphraseNumbers { get; set; }
            public bool? PassphraseSpecial { get; set; }
        }
    }
}