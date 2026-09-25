using System;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager
{
    /// <summary>
    /// Cryptographically secure password generation for the Generator page.
    /// </summary>
    public static class PasswordGenerator
    {
        private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
        private const string DigitChars = "0123456789";
        private const string SymbolChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";

        public const int DefaultLength = 20;
        public const int MinLength = 4;
        public const int MaxLength = 64;

        public sealed class Options
        {
            public int Length { get; set; } = DefaultLength;
            public bool Uppercase { get; set; } = true;
            public bool Lowercase { get; set; } = true;
            public bool Digits { get; set; } = true;
            public bool Symbols { get; set; } = true;
        }

        /// Builds a password using the given options. At least one character set must be enabled.
        public static string Generate(Options options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            int length = Math.Clamp(options.Length, MinLength, MaxLength);
            var required = new System.Collections.Generic.List<string>();
            var alphabet = new StringBuilder();

            if (options.Uppercase)
            {
                required.Add(UppercaseChars);
                alphabet.Append(UppercaseChars);
            }

            if (options.Lowercase)
            {
                required.Add(LowercaseChars);
                alphabet.Append(LowercaseChars);
            }

            if (options.Digits)
            {
                required.Add(DigitChars);
                alphabet.Append(DigitChars);
            }

            if (options.Symbols)
            {
                required.Add(SymbolChars);
                alphabet.Append(SymbolChars);
            }

            if (alphabet.Length == 0)
            {
                throw new InvalidOperationException("At least one character set must be enabled.");
            }

            var result = new StringBuilder(length);

            // Guarantee at least one character from each selected set when length allows.
            int guaranteed = Math.Min(required.Count, length);
            for (int i = 0; i < guaranteed; i++)
            {
                result.Append(Pick(required[i]));
            }

            string pool = alphabet.ToString();
            while (result.Length < length)
            {
                result.Append(Pick(pool));
            }

            return Shuffle(result.ToString());
        }

        /// Convenience overload matching Stage 2 call sites (all sets on, default length).
        public static string Generate(int length = DefaultLength)
        {
            return Generate(new Options { Length = length });
        }

        private static char Pick(string alphabet)
        {
            int index = RandomNumberGenerator.GetInt32(alphabet.Length);
            return alphabet[index];
        }

        private static string Shuffle(string value)
        {
            char[] chars = value.ToCharArray();
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }
    }
}