using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager
{
    /// <summary>
    /// Cryptographically secure, memorable username generation for the Generator page.
    /// Does not use vault data — only a fixed word list and secure random selection.
    /// </summary>
    public static class UsernameGenerator
    {
        public const int DefaultWordCount = 2;
        public const int MinWordCount = 1;
        public const int MaxWordCount = 4;
        public const int MinLengthLimit = 4;
        public const int MaxLengthLimit = 64;

        public enum Style
        {
            WordNumber,
            TwoWords,
            Random
        }

        public enum SeparatorKind
        {
            Dot,
            Underscore,
            Hyphen,
            None
        }

        public enum Capitalization
        {
            Lowercase,
            Capitalized,
            MixedCase
        }

        public sealed class Options
        {
            public Style Style { get; set; } = Style.WordNumber;
            public int WordCount { get; set; } = DefaultWordCount;
            public SeparatorKind Separator { get; set; } = SeparatorKind.Dot;
            public Capitalization Capitalization { get; set; } = Capitalization.Lowercase;
            public int? LengthLimit { get; set; }
            public bool IncludeNumbers { get; set; } = true;
        }

        // Short, readable English words suitable for usernames (no personal data).
        private static readonly string[] Words =
        {
            "amber", "angel", "apple", "aqua", "arrow", "atlas", "autumn", "azure",
            "badge", "baker", "bamboo", "beach", "berry", "bird", "blaze", "bloom",
            "blue", "bold", "breeze", "bright", "brook", "bubble", "bunny", "byte",
            "cactus", "candy", "canvas", "canyon", "cedar", "cherry", "chili", "cipher",
            "city", "clear", "cliff", "cloud", "clover", "coast", "cobalt", "comet",
            "coral", "cosmic", "coyote", "crane", "creek", "crisp", "crown", "crystal",
            "daisy", "delta", "dew", "diamond", "diesel", "doodle", "dove", "dragon",
            "drift", "dusk", "eagle", "echo", "ember", "emerald", "falcon", "fern",
            "field", "finch", "flame", "flash", "flint", "flora", "fog", "forest",
            "fox", "frost", "galaxy", "garden", "glade", "glow", "golden", "grape",
            "green", "grove", "harbor", "hawk", "haze", "heron", "honey", "horizon",
            "ice", "indigo", "iris", "ivory", "jade", "jazz", "jelly", "jungle",
            "kernel", "kite", "kiwi", "koala", "lake", "leaf", "lemon", "light",
            "lilac", "lime", "lion", "lotus", "lunar", "maple", "marble", "meadow",
            "melody", "mint", "mist", "moon", "moss", "mountain", "navy", "nectar",
            "neon", "night", "nova", "oak", "ocean", "olive", "onyx", "orange",
            "orbit", "otter", "owl", "panda", "peach", "pearl", "pebble", "pepper",
            "petal", "phoenix", "pine", "pixel", "plume", "pond", "prism", "pulse",
            "quartz", "quiet", "quill", "radar", "rain", "raven", "reef", "ridge",
            "river", "robin", "rocket", "rose", "ruby", "sage", "sand", "scarlet",
            "shadow", "shore", "silver", "sky", "slate", "snow", "solar", "spark",
            "sprite", "star", "stone", "storm", "sugar", "sun", "swift", "tide",
            "tiger", "timber", "topaz", "trail", "tree", "tulip", "valley", "violet",
            "wave", "willow", "wind", "winter", "wolf", "wood", "zen", "zinc"
        };

        public static string Generate(Options options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            int wordCount = Math.Clamp(options.WordCount, MinWordCount, MaxWordCount);
            string separator = SeparatorToString(options.Separator);
            bool numbers = options.IncludeNumbers;

            // A few attempts in case a random draw exceeds the length limit.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                string candidate = options.Style switch
                {
                    Style.TwoWords => BuildFromWords(2, separator, options.Capitalization, numbers),
                    Style.Random => BuildFromWords(wordCount, separator, options.Capitalization, numbers),
                    _ => BuildWordNumber(options.Capitalization, numbers)
                };

                if (options.LengthLimit is int limit && limit >= MinLengthLimit)
                {
                    limit = Math.Clamp(limit, MinLengthLimit, MaxLengthLimit);
                    if (candidate.Length > limit)
                    {
                        candidate = TrimToLength(candidate, limit);
                    }
                }

                if (!string.IsNullOrEmpty(candidate))
                {
                    return candidate;
                }
            }

            // Fallback: short simple username.
            return ApplyCapitalization(PickWord(), options.Capitalization) +
                   (numbers ? RandomNumberGenerator.GetInt32(10, 100).ToString(CultureInfo.InvariantCulture) : "");
        }

        private static string BuildWordNumber(Capitalization capitalization, bool includeNumbers)
        {
            string word = ApplyCapitalization(PickWord(), capitalization);
            if (!includeNumbers)
            {
                return word;
            }

            int digits = RandomNumberGenerator.GetInt32(2, 4); // 2–3 digit suffix
            int max = (int)Math.Pow(10, digits);
            int min = (int)Math.Pow(10, digits - 1);
            int n = RandomNumberGenerator.GetInt32(min, max);
            return word + n.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildFromWords(int count, string separator, Capitalization capitalization, bool includeNumbers)
        {
            count = Math.Clamp(count, 1, MaxWordCount);
            var parts = new string[count];
            for (int i = 0; i < count; i++)
            {
                parts[i] = ApplyCapitalization(PickWord(), capitalization);
            }

            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0 && !string.IsNullOrEmpty(separator))
                {
                    sb.Append(separator);
                }

                sb.Append(parts[i]);
            }

            if (includeNumbers)
            {
                sb.Append(RandomNumberGenerator.GetInt32(10, 100).ToString(CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        private static string TrimToLength(string value, int limit)
        {
            if (value.Length <= limit)
            {
                return value;
            }

            // Prefer keeping trailing digits if present.
            int digitStart = value.Length;
            while (digitStart > 0 && char.IsDigit(value[digitStart - 1]))
            {
                digitStart--;
            }

            string digits = value.Substring(digitStart);
            string head = value.Substring(0, digitStart);
            int headBudget = Math.Max(0, limit - digits.Length);
            if (headBudget <= 0)
            {
                return value.Substring(0, limit);
            }

            if (head.Length > headBudget)
            {
                head = head.Substring(0, headBudget).TrimEnd('.', '_', '-');
            }

            string result = head + digits;
            return result.Length > limit ? result.Substring(0, limit) : result;
        }

        private static string SeparatorToString(SeparatorKind kind)
        {
            return kind switch
            {
                SeparatorKind.Underscore => "_",
                SeparatorKind.Hyphen => "-",
                SeparatorKind.None => "",
                _ => "."
            };
        }

        private static string ApplyCapitalization(string word, Capitalization mode)
        {
            if (string.IsNullOrEmpty(word))
            {
                return word;
            }

            return mode switch
            {
                Capitalization.Capitalized => char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant(),
                Capitalization.MixedCase => MixCase(word),
                _ => word.ToLowerInvariant()
            };
        }

        private static string MixCase(string word)
        {
            var chars = word.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (RandomNumberGenerator.GetInt32(0, 2) == 1)
                {
                    chars[i] = char.ToUpperInvariant(chars[i]);
                }
            }

            return new string(chars);
        }

        private static string PickWord()
        {
            int index = RandomNumberGenerator.GetInt32(Words.Length);
            return Words[index];
        }
    }
}
