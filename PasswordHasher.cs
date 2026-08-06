using System;
using System.Security.Cryptography;

namespace PasswordManager
{
    /// <summary>
    /// Hashes and verifies passwords using PBKDF2 (Rfc2898DeriveBytes).
    /// PBKDF2 is built into .NET's System.Security.Cryptography namespace, so it requires
    /// no third-party dependencies, is NIST-approved, and its iteration count can be tuned
    /// upward over time to keep pace with faster hardware and resist brute-force attacks.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSizeBytes = 16;   // 128-bit salt
        private const int HashSizeBytes = 32;   // 256-bit derived key
        private const int Iterations = 210_000; // In line with current OWASP guidance for PBKDF2-HMAC-SHA256

        /// <summary>
        /// Hashes a password with a freshly generated random salt.
        /// The result is a single self-describing string ("iterations.salt.hash", Base64-encoded
        /// parts) so it can be stored as one value - e.g. one dictionary entry or database column -
        /// and verified later without needing the salt or iteration count stored separately.
        /// </summary>
        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifies a plaintext password attempt against a hash produced by HashPassword.
        /// Recomputes the hash using the stored salt and iteration count, then compares it
        /// to the stored hash using a fixed-time comparison to avoid leaking timing information.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            string[] parts = storedHash.Split('.', 3);
            if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations))
            {
                return false; // Malformed stored hash; fail closed rather than throw
            }

            byte[] salt = Convert.FromBase64String(parts[1]);
            byte[] expectedHash = Convert.FromBase64String(parts[2]);
            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
    }
}