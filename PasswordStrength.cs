using System.Drawing;
using System.Linq;

namespace PasswordManager
{
    /// Password-strength scoring, shared between AddEntryForm's strength meter (shown while
    /// typing/generating a password) and MainForm's details panel (shown for an existing
    /// entry's saved password) - extracted so both compute strength the same way instead of
    /// each having their own copy of the same heuristic.
    public static class PasswordStrength
    {
        /// <summary>
        /// Simple, transparent heuristic - not a full entropy-based analysis, just length
        /// plus character variety. Six criteria total, mapped to a 0-100 score and a tier.
        /// </summary>
        public static (int score, string label, Color color) Evaluate(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return (0, "", Color.Gray);
            }

            int criteriaMet = 0;
            if (password.Length >= 8) criteriaMet++;
            if (password.Length >= 12) criteriaMet++;
            if (password.Any(char.IsLower)) criteriaMet++;
            if (password.Any(char.IsUpper)) criteriaMet++;
            if (password.Any(char.IsDigit)) criteriaMet++;
            if (password.Any(c => !char.IsLetterOrDigit(c))) criteriaMet++;

            int score = (int)(criteriaMet / 6.0 * 100);

            if (criteriaMet <= 2)
            {
                return (score, "Weak", Color.IndianRed);
            }
            if (criteriaMet <= 4)
            {
                return (score, "Fair", Color.Orange);
            }
            if (criteriaMet == 5)
            {
                return (score, "Good", Color.Goldenrod);
            }
            return (score, "Strong", Color.SeaGreen);
        }
    }
}