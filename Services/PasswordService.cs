using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

namespace Nimbus_Internet_Blocker.Services
{
    /*
     * RecoveryMode enum
     * Defines the two recovery modes available to the user
     * Accountability = Answer 5 questions + quote of the day
     * Guardian       = Transcribe a one-time generated hash code
     */
    public enum RecoveryMode
    {
        Accountability,
        Guardian
    }

    public sealed class PasswordService : IPasswordService
    {
        // ── Preference Keys ────────────────────────────────────────────────────
        /*
         * These are the keys used to store and retrieve values from MAUI Preferences
         * Preferences is a built-in MAUI key-value store that persists between app sessions
         * Think of it like a dictionary that never forgets
         */
        private const string PREF_HASH = "nimbus_password_hash";     // Stores the hashed password string
        private const string PREF_ENABLED = "nimbus_password_enabled";  // Stores whether a password is set
        private const string PREF_RECOVERY = "nimbus_recovery_mode";     // Stores the chosen recovery mode

        // ── Password State ─────────────────────────────────────────────────────

        /*
         * IsPasswordEnabled()
         * Checks if the user has set up a password
         * Returns false by default if the key has never been set
         */
        public bool IsPasswordEnabled()
            => Preferences.Get(PREF_ENABLED, false);

        /*
         * GetRecoveryMode()
         * Reads the stored recovery mode string from Preferences and converts
         * it back into the RecoveryMode enum. Defaults to Accountability if
         * the value is missing or unrecognized
         */
        public RecoveryMode GetRecoveryMode()
        {
            var stored = Preferences.Get(PREF_RECOVERY, "Accountability");
            return Enum.TryParse<RecoveryMode>(stored, out var mode) ? mode : RecoveryMode.Accountability;
        }

        /*
         * SetRecoveryMode()
         * Saves the chosen recovery mode to Preferences as a string
         * Called from the Settings page when the user picks their recovery method
         */
        public void SetRecoveryMode(RecoveryMode mode)
            => Preferences.Set(PREF_RECOVERY, mode.ToString());

        // ── Password Management ────────────────────────────────────────────────

        /*
         * SetPasswordAsync()
         * Validates the new password against all rules, hashes it using
         * PasswordHasher<string> (PBKDF2 with random salt), and stores
         * the resulting hash string in Preferences
         *
         * Returns (true, success message) or (false, error message)
         * Never throws — all failures come back as a false result with a message
         */
        public Task<(bool success, string message)> SetPasswordAsync(
            string password, string confirmPassword, RecoveryMode recoveryMode)
        {
            // ── Validation ─────────────────────────────────────────────────────

            if (password != confirmPassword)
                return Task.FromResult((false, "Passwords do not match."));

            if (password.Length < 8)
                return Task.FromResult((false, "Password must be at least 8 characters."));

            // Any() checks if at least one character satisfies the condition
            // All() would require every character to satisfy it which is wrong here
            if (!password.Any(char.IsLetter))
                return Task.FromResult((false, "Password must contain at least one letter."));

            if (!password.Any(char.IsDigit))
                return Task.FromResult((false, "Password must contain at least one number."));

            // If every character is a letter or digit then there are no special characters
            if (password.All(char.IsLetterOrDigit))
                return Task.FromResult((false, "Password must contain at least one special character."));

            // ── Hashing ────────────────────────────────────────────────────────
            /*
             * PasswordHasher uses PBKDF2 with a random salt internally
             * The salt and hash are combined into one Base64 string automatically
             * "nimbus" is the user object — we don't have real user objects so
             * we pass a constant string as a stand-in
             */
            var hash = new PasswordHasher<string>().HashPassword("nimbus", password);

            // ── Save to Preferences ────────────────────────────────────────────
            Preferences.Set(PREF_HASH, hash);
            Preferences.Set(PREF_ENABLED, true);
            Preferences.Set(PREF_RECOVERY, recoveryMode.ToString());

            return Task.FromResult((true, "Password set successfully."));
        }

        /*
         * VerifyPassword()
         * Hashes the attempt using the same salt stored in the hash string
         * and compares the result. Returns true if they match, false otherwise.
         *
         * SuccessRehashNeeded means the password matched but was hashed with
         * older parameters — we treat it as a success since we are not
         * upgrading hashes in this version
         */
        public bool VerifyPassword(string attempt)
        {
            // If no password is set there is nothing to verify
            if (!IsPasswordEnabled()) return false;

            var storedHash = Preferences.Get(PREF_HASH, string.Empty);

            var result = new PasswordHasher<string>()
                .VerifyHashedPassword("nimbus", storedHash, attempt);

            return result == PasswordVerificationResult.Success ||
                   result == PasswordVerificationResult.SuccessRehashNeeded;
        }

        /*
         * ClearPasswordAsync()
         * Removes the password hash and resets the enabled flag
         * Called from Settings when the user removes their password
         * Sets PREF_ENABLED to false rather than removing it so
         * IsPasswordEnabled() always has a value to read
         */
        public Task ClearPasswordAsync()
        {
            Preferences.Remove(PREF_HASH);
            Preferences.Set(PREF_ENABLED, false);
            Preferences.Remove(PREF_RECOVERY);
            return Task.CompletedTask;
        }

        // ── Guardian Hash Generation ───────────────────────────────────────────

        /*
         * GenerateGuardianHash()
         * Generates a cryptographically random hash in the format:
         * xxxxxx-xxxxxx-xxxxxx-xxxxxx
         * Each segment is 6 characters and guaranteed to contain at least
         * one lowercase letter, one uppercase letter, one digit, and one
         * special character — then shuffled so the order is unpredictable
         *
         * Uses RandomNumberGenerator (cryptographically secure) not System.Random
         * This hash is NEVER stored — it is shown once and discarded
         */
        public string GenerateGuardianHash()
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";
            const string special = "!@#$%^&*()";
            const string all = lowercase + uppercase + numbers + special; // Full pool for remaining spots

            const int segmentLength = 6;
            const int segmentCount = 4;

            var segments = new string[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                var chars = new char[segmentLength];

                // Guarantee one of each character type in every segment
                chars[0] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
                chars[1] = numbers[RandomNumberGenerator.GetInt32(numbers.Length)];
                chars[2] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
                chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

                // Fill the remaining 2 spots from the full combined pool
                chars[4] = all[RandomNumberGenerator.GetInt32(all.Length)];
                chars[5] = all[RandomNumberGenerator.GetInt32(all.Length)];

                // Shuffle so the guaranteed characters are not always in the same positions
                RandomNumberGenerator.Shuffle(chars.AsSpan());

                segments[i] = new string(chars);
            }

            // Join the 4 segments with a dash separator
            // Example output: mXq3!a-B9@kRz-7#wPnE-Ld&2Yf
            return string.Join("-", segments);
        }
    }
}