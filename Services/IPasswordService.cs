using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Services
{
    /*
     * IPasswordService
     * Defines the contract for all password-related operations in Nimbus.
     * Program against this interface everywhere — never depend on PasswordService directly.
     * This makes the service swappable and testable.
     *
     * Two protection modes:
     *   Accountability — no password. Stores only a flag. Unlocked by answering 5 questions.
     *   Guardian       — stores a PBKDF2 password hash. Recovery via a one-time hash code.
     */
    public interface IPasswordService
    {
        // ── Mode State ─────────────────────────────────────────────────────────

        /*
         * IsPasswordEnabled()
         * Returns true only when Guardian mode is active (a password hash is stored).
         * Returns false in Accountability mode and when nothing is set.
         * Used by UnlockModal and Blocking.razor to decide whether to show the lock prompt.
         */
        bool IsPasswordEnabled();

        /*
         * IsAccountabilityModeActive()
         * Returns true when the user has activated Accountability mode.
         * In this state IsPasswordEnabled() is false — there is no password to verify.
         * The UnlockModal routes to AccountabilityFlow when this is true.
         */
        bool IsAccountabilityModeActive();

        /*
         * GetRecoveryMode()
         * Returns Accountability if the accountability flag is set, Guardian otherwise.
         * Used by UnlockModal to decide which recovery flow to show.
         */
        RecoveryMode GetRecoveryMode();

        // ── Mode Setup ─────────────────────────────────────────────────────────

        /*
         * SetAccountabilityModeAsync()
         * Activates Accountability mode.
         * Stores only the accountability flag — never touches a password hash.
         * Clears any existing Guardian password so the modes are mutually exclusive.
         */
        Task SetAccountabilityModeAsync();

        /*
         * SetPasswordAsync()
         * Guardian mode only — validates, hashes, and stores a new password.
         * Always marks the mode as Guardian and clears the accountability flag.
         * During initial Guardian setup, pass the one-time recovery code so its
         * PBKDF2 hash is stored for later verification. Omit it when merely changing
         * the password — the existing recovery code is preserved.
         * Returns (true, success message) or (false, error message).
         * Never throws — all failures come back as a false result with a message.
         */
        Task<(bool success, string message)> SetPasswordAsync(
            string password,
            string confirmPassword,
            string? recoveryCode = null);

        // ── Password Operations ────────────────────────────────────────────────

        /*
         * VerifyPassword()
         * Checks a password attempt against the stored Guardian hash.
         * Returns true if correct, false if wrong or no password is set.
         * Never called in Accountability mode — the flow uses question answers instead.
         */
        bool VerifyPassword(string attempt);

        /*
         * VerifyRecoveryCode()
         * Checks a typed recovery code against the PBKDF2 hash stored during Guardian
         * setup. Returns true only on an exact match. Returns false when no recovery
         * hash is stored (Guardian mode set up before recovery verification existed)
         * or when the code is wrong.
         */
        bool VerifyRecoveryCode(string attempt);

        /*
         * ClearPasswordAsync()
         * Removes all stored protection state — hash, enabled flag, accountability flag,
         * and recovery mode. Resets the app to the unprotected state.
         * Called after the user passes the unlock flow in Settings.
         */
        Task ClearPasswordAsync();

        // ── Guardian Recovery ──────────────────────────────────────────────────

        /*
         * GenerateGuardianHash()
         * Generates a cryptographically random one-time recovery code in the format
         * xxxxxx-xxxxxx-xxxxxx-xxxxxx. The plaintext code is shown once during Guardian
         * setup and never stored; its PBKDF2 hash is persisted (see SetPasswordAsync)
         * so a typed code can be verified later via VerifyRecoveryCode.
         */
        string GenerateGuardianHash();
    }
}
