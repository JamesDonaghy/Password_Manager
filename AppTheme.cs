using System;
using System.Drawing;
using Microsoft.Win32;

namespace PasswordManager
{
    /// <summary>
    /// Central colour palette and typography. Forms pull colours from here rather than
    /// hard-coding values so the look stays consistent.
    ///
    /// Theme foundation (Light / Dark / System):
    /// - CurrentPreference is the user's saved choice.
    /// - IsDark is the resolved effective mode (System follows Windows).
    /// - Active surface tokens still expose the existing light palette so the rest of
    ///   the app is unchanged until later commits migrate controls to dark.
    /// - Dark palette constants are defined here for those follow-up commits.
    /// </summary>
    public static class AppTheme
    {
        public enum Preference
        {
            Light = 0,
            Dark = 1,
            System = 2
        }

        // Active tokens (existing light theme — unchanged for this foundation commit)
        public static readonly Color Background = Color.FromArgb(0xF5, 0xF6, 0xFA);
        public static readonly Color PanelBackground = Color.FromArgb(0xFA, 0xFA, 0xFC);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(0xE3, 0xE5, 0xEA);
        public static readonly Color Accent = Color.FromArgb(0x6C, 0x5C, 0xE7);
        public static readonly Color AccentHover = Color.FromArgb(0x59, 0x4A, 0xC9);
        public static readonly Color Danger = Color.FromArgb(0xD9, 0x4A, 0x4A);
        public static readonly Color DangerHover = Color.FromArgb(0xB8, 0x3A, 0x3A);
        public static readonly Color AccentSubtle = Color.FromArgb(0xED, 0xEB, 0xFC);
        public static readonly Color TextPrimary = Color.FromArgb(0x20, 0x22, 0x2A);
        public static readonly Color TextSecondary = Color.FromArgb(0x6B, 0x72, 0x80);

        // Dark palette (ready for later commits — not wired to most UI yet)
        public static readonly Color DarkBackground = Color.FromArgb(0x17, 0x19, 0x23);
        public static readonly Color DarkPanelBackground = Color.FromArgb(0x1C, 0x1E, 0x2A);
        public static readonly Color DarkSurface = Color.FromArgb(0x22, 0x26, 0x33);
        public static readonly Color DarkBorder = Color.FromArgb(0x35, 0x3B, 0x4B);
        public static readonly Color DarkAccent = Color.FromArgb(0x8B, 0x7C, 0xF6);
        public static readonly Color DarkAccentHover = Color.FromArgb(0xA5, 0x99, 0xF7);
        public static readonly Color DarkAccentSubtle = Color.FromArgb(0x2A, 0x27, 0x45);
        public static readonly Color DarkTextPrimary = Color.FromArgb(0xF3, 0xF4, 0xF6);
        public static readonly Color DarkTextSecondary = Color.FromArgb(0xA8, 0xB0, 0xC0);
        public static readonly Color DarkDanger = Color.FromArgb(0xE8, 0x6A, 0x6A);
        public static readonly Color DarkDangerHover = Color.FromArgb(0xD9, 0x4A, 0x4A);

        private static Preference preference = Preference.System;
        private static bool isDark;
        private static bool initialized;
        private static System.Windows.Forms.Timer systemWatchTimer;

        public static event EventHandler Changed;

        public static bool IsDark => isDark;

        public static Preference CurrentPreference
        {
            get
            {
                EnsureInitialized();
                return preference;
            }
            set
            {
                EnsureInitialized();
                if (preference == value)
                {
                    return;
                }

                preference = value;
                AppPreferences.ThemePreference = value;
                Recalculate(notify: true, forceNotify: true);
            }
        }

        private const string FontFamilyName = "Segoe UI";

        public static Font Base => new Font(FontFamilyName, 9.5f);
        public static Font Heading => new Font(FontFamilyName, 14f, FontStyle.Bold);
        public static Font Caption => new Font(FontFamilyName, 9f, FontStyle.Bold);

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            preference = AppPreferences.ThemePreference;
            Recalculate(notify: false);
            EnsureSystemWatch();
        }

        private static void EnsureInitialized()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private static void EnsureSystemWatch()
        {
            if (systemWatchTimer != null)
            {
                return;
            }

            systemWatchTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            systemWatchTimer.Tick += (s, e) =>
            {
                if (preference == Preference.System)
                {
                    Recalculate(notify: true);
                }
            };
            systemWatchTimer.Start();
        }

        private static void Recalculate(bool notify, bool forceNotify = false)
        {
            bool dark = preference switch
            {
                Preference.Dark => true,
                Preference.Light => false,
                _ => IsWindowsAppsDarkMode()
            };

            bool changed = dark != isDark;
            isDark = dark;

            if (notify && (changed || forceNotify))
            {
                Changed?.Invoke(null, EventArgs.Empty);
            }
        }

        public static bool IsWindowsAppsDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int value)
                    {
                        return value == 0;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        // Tokens for the foundation preview surface only.
        public static Color PreviewBackground => isDark ? DarkBackground : Background;
        public static Color PreviewSurface => isDark ? DarkSurface : Surface;
        public static Color PreviewBorder => isDark ? DarkBorder : Border;
        public static Color PreviewTextPrimary => isDark ? DarkTextPrimary : TextPrimary;
        public static Color PreviewTextSecondary => isDark ? DarkTextSecondary : TextSecondary;
        public static Color PreviewAccent => isDark ? DarkAccent : Accent;
        public static Color PreviewAccentSubtle => isDark ? DarkAccentSubtle : AccentSubtle;
    }
}