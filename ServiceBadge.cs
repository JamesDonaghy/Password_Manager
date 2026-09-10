using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace PasswordManager
{
    /// Shared logic for the coloured service-initial badge shown both in the entry list
    /// (AccountGridPresenter) and the details panel header (MainForm) - kept in one place
    /// so a given service always gets the same badge wherever it appears, rather than each
    /// place computing its own colour independently.
    ///
    /// This is a stand-in for a real per-service icon system (fetching/caching actual brand
    /// icons), which is a bigger feature for later - in the same spirit as a contact app's
    /// coloured-initial avatars for contacts with no photo.
    public static class ServiceBadge
    {
        private static readonly Color[] Colors =
        {
            AppTheme.Accent,
            Color.FromArgb(0xE0, 0x6C, 0x3D), // Orange
            Color.FromArgb(0x2F, 0x9E, 0x6B), // Green
            Color.FromArgb(0xD1, 0x4B, 0x7A), // Rose
            Color.FromArgb(0x3B, 0x82, 0xC4), // Blue
            Color.FromArgb(0x8A, 0x5C, 0xD6), // Violet
            Color.FromArgb(0xC4, 0x8A, 0x2F), // Amber
            Color.FromArgb(0x4F, 0xA8, 0xA3), // Teal
        };

        public static Color ColorFor(string serviceName)
        {
            return Colors[StableHash(serviceName ?? string.Empty) % Colors.Length];
        }

        public static string InitialFor(string serviceName)
        {
            return string.IsNullOrWhiteSpace(serviceName) ? "?" : serviceName.Trim().Substring(0, 1).ToUpperInvariant();
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        // A small, stable (non-randomised) hash so the same service name always maps to the
        // same badge colour, including across app restarts. string.GetHashCode() itself is
        // randomised per-process in modern .NET (to resist hash-flooding attacks) and isn't
        // safe to use for anything that needs to stay consistent between runs.
        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in value)
                {
                    hash = hash * 31 + c;
                }

                return hash & int.MaxValue; // Clear the sign bit so % always returns a non-negative index
            }
        }
    }
}