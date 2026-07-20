using Nimbus_Internet_Blocker.Utilities;
using System.Security.Cryptography;

namespace Nimbus_Internet_Blocker.Services
{
    /*
     * RecoveryMode enum
     * Accountability = Answer 5 questions + type today's quote — no password required.
     * Guardian       = Transcribe a one-time hash stored with a trusted contact.
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
         * All state lives in MAUI Preferences — a built-in key/value store that
         * persists between app sessions on the device. Think of it as a typed
         * dictionary that never forgets.
         */
        private const string PREF_HASH           = "nimbus_password_hash";
        private const string PREF_ENABLED        = "nimbus_password_enabled";
        private const string PREF_ACCOUNTABILITY = "nimbus_accountability_active";
        private const string PREF_RECOVERY       = "nimbus_recovery_mode";
        private const string PREF_RECOVERY_HASH  = "nimbus_guardian_recovery_hash";

        // ── Mode State ─────────────────────────────────────────────────────────

        /*
         * IsPasswordEnabled()
         * True only when Guardian mode is active — a hash has been stored.
         * False for Accountability mode and when nothing is configured.
         */
        public bool IsPasswordEnabled()
            => Preferences.Get(PREF_ENABLED, false);

        /*
         * IsAccountabilityModeActive()
         * True when the user has chosen Accountability mode.
         * In this state there is no password — the unlock path goes through questions.
         */
        public bool IsAccountabilityModeActive()
            => Preferences.Get(PREF_ACCOUNTABILITY, false);

        /*
         * GetRecoveryMode()
         * Reads the active mode from Preferences.
         * Checks the accountability flag first because it is the authoritative source
         * for that mode — the PREF_RECOVERY string is a secondary record.
         */
        public RecoveryMode GetRecoveryMode()
        {
            if (IsAccountabilityModeActive()) return RecoveryMode.Accountability;

            var stored = Preferences.Get(PREF_RECOVERY, nameof(RecoveryMode.Guardian));
            return Enum.TryParse<RecoveryMode>(stored, out var mode)
                ? mode
                : RecoveryMode.Guardian;
        }

        // ── Mode Setup ─────────────────────────────────────────────────────────

        /*
         * SetAccountabilityModeAsync()
         * Activates Accountability mode by writing only the flag.
         * Clears any existing Guardian hash so the two modes cannot coexist.
         * IsPasswordEnabled() will return false after this call.
         */
        public Task SetAccountabilityModeAsync()
        {
            Preferences.Remove(PREF_HASH);
            Preferences.Set(PREF_ENABLED, false);
            Preferences.Set(PREF_ACCOUNTABILITY, true);
            Preferences.Set(PREF_RECOVERY, nameof(RecoveryMode.Accountability));
            return Task.CompletedTask;
        }

        /*
         * SetPasswordAsync()
         * Guardian mode only. Validates the password against all rules,
         * hashes it via Utilities/PasswordHash (PBKDF2, self-describing
         * v1.iterations.salt.subkey string), and stores the result in Preferences.
         * Always records the mode as Guardian and clears the accountability flag.
         *
         * When recoveryCode is provided (initial Guardian setup), its PBKDF2 hash
         * is also stored so it can be verified later via VerifyRecoveryCode. When
         * omitted (password-change path), the existing recovery hash is preserved.
         *
         * Returns (true, success message) or (false, validation error message).
         * Never throws — all failures come back as a false result.
         */
        public Task<(bool success, string message)> SetPasswordAsync(
            string password, string confirmPassword, string? recoveryCode = null)
        {
            // ── Validation ─────────────────────────────────────────────────────

            if (password != confirmPassword)
                return Task.FromResult((false, "Passwords do not match."));

            if (password.Length < 8)
                return Task.FromResult((false, "Password must be at least 8 characters."));

            if (!password.Any(char.IsLetter))
                return Task.FromResult((false, "Password must contain at least one letter."));

            if (!password.Any(char.IsDigit))
                return Task.FromResult((false, "Password must contain at least one number."));

            if (password.All(char.IsLetterOrDigit))
                return Task.FromResult((false, "Password must contain at least one special character."));

            // ── Hashing ────────────────────────────────────────────────────────
            // PBKDF2 via Utilities/PasswordHash — a self-describing
            // v1.iterations.salt.subkey string; see that file for the format.
            var hash = PasswordHash.Hash(password);

            // ── Persist ────────────────────────────────────────────────────────
            Preferences.Set(PREF_HASH, hash);
            Preferences.Set(PREF_ENABLED, true);
            Preferences.Set(PREF_ACCOUNTABILITY, false);   // clear accountability flag
            Preferences.Set(PREF_RECOVERY, nameof(RecoveryMode.Guardian));

            // Store the recovery-code hash only during initial Guardian setup.
            // Change-password calls pass no recoveryCode, preserving the original code
            // the guardian already holds.
            if (!string.IsNullOrWhiteSpace(recoveryCode))
            {
                var recoveryHash = PasswordHash.Hash(recoveryCode);
                Preferences.Set(PREF_RECOVERY_HASH, recoveryHash);
            }

            return Task.FromResult((true, "Password set successfully."));
        }

        // ── Password Operations ────────────────────────────────────────────────

        /*
         * VerifyPassword()
         * Verifies the attempt against the stored hash via PasswordHash.Verify —
         * PBKDF2 with the salt/iteration count embedded in the stored string.
         */
        public bool VerifyPassword(string attempt)
        {
            if (!IsPasswordEnabled()) return false;

            var storedHash = Preferences.Get(PREF_HASH, string.Empty);
            return PasswordHash.Verify(attempt, storedHash);
        }

        /*
         * VerifyRecoveryCode()
         * Verifies the typed code against the stored recovery hash via
         * PasswordHash.Verify. Trims surrounding whitespace but preserves case
         * and dashes, which are significant. Returns false when no recovery
         * hash is stored.
         */
        public bool VerifyRecoveryCode(string attempt)
        {
            var storedHash = Preferences.Get(PREF_RECOVERY_HASH, string.Empty);
            if (string.IsNullOrEmpty(storedHash)) return false;

            return PasswordHash.Verify(attempt.Trim(), storedHash);
        }

        /*
         * ClearPasswordAsync()
         * Wipes all protection state from Preferences.
         * Sets PREF_ENABLED and PREF_ACCOUNTABILITY to false (rather than removing)
         * so the boolean reads always have a default to fall back on.
         * After this call IsPasswordEnabled() and IsAccountabilityModeActive() both return false.
         */
        public Task ClearPasswordAsync()
        {
            Preferences.Remove(PREF_HASH);
            Preferences.Set(PREF_ENABLED, false);
            Preferences.Set(PREF_ACCOUNTABILITY, false);
            Preferences.Remove(PREF_RECOVERY);
            Preferences.Remove(PREF_RECOVERY_HASH);
            return Task.CompletedTask;
        }

        // ── Guardian Hash Generation ───────────────────────────────────────────

        /*
         * GenerateGuardianHash()
         * Produces a cryptographically random recovery code in the format:
         * xxxxxx-xxxxxx-xxxxxx-xxxxxx
         *
         * Each 6-character segment is guaranteed to contain at least one lowercase
         * letter, one uppercase letter, one digit, and one special character.
         * The characters are then shuffled so the guaranteed positions are unpredictable.
         *
         * Uses RandomNumberGenerator (CSPRNG) — never System.Random.
         * The plaintext code is shown once during setup and never stored; its
         * PBKDF2 hash is persisted by SetPasswordAsync so it can be verified
         * later via VerifyRecoveryCode.
         */
        public string GenerateGuardianHash()
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers   = "0123456789";
            const string special   = "!@#$%^&*()";
            const string all       = lowercase + uppercase + numbers + special;

            const int segmentLength = 6;
            const int segmentCount  = 4;

            var segments = new string[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                var chars = new char[segmentLength];

                chars[0] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
                chars[1] = numbers[RandomNumberGenerator.GetInt32(numbers.Length)];
                chars[2] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
                chars[3] = special[RandomNumberGenerator.GetInt32(special.Length)];
                chars[4] = all[RandomNumberGenerator.GetInt32(all.Length)];
                chars[5] = all[RandomNumberGenerator.GetInt32(all.Length)];

                RandomNumberGenerator.Shuffle(chars.AsSpan());

                segments[i] = new string(chars);
            }

            return string.Join("-", segments);
        }
    }
}
