using System.Drawing;

namespace PasswordManager
{
    /// Central place for the app's colour palette and typography. Forms should pull
    /// from here rather than hard-coding System.Drawing.Color/Font values directly,
    /// so the look stays consistent as more of the UI is modernised and can be
    /// adjusted app-wide from one place.
    ///
    /// This is intentionally just constants - no styling logic lives here yet. As
    /// more forms adopt the theme, shared helpers (e.g. "style this button as
    /// primary") can be added, but there's no need to build that ahead of having a
    /// second real use case for it.
    public static class AppTheme
    {
        // Base surface behind the main window content.
        public static readonly Color Background = Color.FromArgb(0xF5, 0xF6, 0xFA);

        // Slightly different shade for panels that should read as distinct regions
        // (nav rail, details pane) without needing a hard border to separate them.
        public static readonly Color PanelBackground = Color.FromArgb(0xFA, 0xFA, 0xFC);

        // Distinct fill for interactive input surfaces (search box, future text fields)
        // that need to read as a clickable field against Background/PanelBackground,
        // rather than blending into whichever panel they sit on.
        public static readonly Color Surface = Color.White;

        // Subtle divider/border colour - for hairline separators between regions
        // (e.g. the nav panel/content divider) instead of WinForms' default 3D borders.
        public static readonly Color Border = Color.FromArgb(0xE3, 0xE5, 0xEA);

        // Single accent colour, used sparingly (primary actions, selection, emphasis).
        public static readonly Color Accent = Color.FromArgb(0x6C, 0x5C, 0xE7);

        // Darker accent shade for hover/pressed states on solid-accent ("primary") buttons,
        // where AccentSubtle (meant for light tint-on-light-background hovers) would be too
        // faint to read as a state change against the accent fill itself.
        public static readonly Color AccentHover = Color.FromArgb(0x59, 0x4A, 0xC9);

        // Light tint of the accent colour - for hover/selected backgrounds where the
        // full accent colour would be too strong (e.g. nav item hover state).
        public static readonly Color AccentSubtle = Color.FromArgb(0xED, 0xEB, 0xFC);

        public static readonly Color TextPrimary = Color.FromArgb(0x20, 0x22, 0x2A);
        public static readonly Color TextSecondary = Color.FromArgb(0x6B, 0x72, 0x80);

        // Segoe UI is the standard modern Windows UI font and is present on every
        // supported Windows version - a plain typography upgrade over the previous
        // Arial/default-font mix, no new font files or dependencies needed.
        private const string FontFamily = "Segoe UI";

        public static Font Base => new Font(FontFamily, 9.5f);
        public static Font Heading => new Font(FontFamily, 14f, FontStyle.Bold);
        public static Font Caption => new Font(FontFamily, 9f, FontStyle.Bold);
    }
}