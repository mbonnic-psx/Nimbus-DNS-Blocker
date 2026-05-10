using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Services
{
    /*
     * IPasswordService
     * Defines the contract for all password-related operations in Nimbus
     * Program against this interface everywhere — never depend on PasswordService directly
     * This makes the service swappable and testable
     */
    public interface IPasswordService
    {
        // ── Password State ─────────────────────────────────────────────────────

        /*
         * IsPasswordEnabled()
         * Returns true if the user has set up a password
         * Used by UnlockModal to decide whether to show the lock prompt
         */
        bool IsPasswordEnabled();

        /*
         * GetRecoveryMode()
         * Returns the recovery mode the user chose during password setup
         * Either Accountability (5 questions + quote) or Guardian (one-time hash)
         */
        RecoveryMode GetRecoveryMode();

        /*
         * SetRecoveryMode()
         * Updates the stored recovery mode
         * Called from Settings if the user wants to switch modes
         */
        void SetRecoveryMode(RecoveryMode mode);

        // ── Password Management ────────────────────────────────────────────────

        /*
         * SetPasswordAsync()
         * Validates, hashes, and stores a new password
         * Returns (true, success message) or (false, error message)
         * Never throws — all failures come back as a false result
         */
        Task<(bool success, string message)> SetPasswordAsync(
            string password,
            string confirmPassword,
            RecoveryMode recoveryMode);

        /*
         * VerifyPassword()
         * Checks a password attempt against the stored hash
         * Returns true if correct, false if wrong or no password is set
         */
        bool VerifyPassword(string attempt);

        /*
         * ClearPasswordAsync()
         * Removes the stored password and resets all password preferences
         * Called from Settings when the user removes their password
         */
        Task ClearPasswordAsync();

        // ── Guardian Recovery ──────────────────────────────────────────────────

        /*
         * GenerateGuardianHash()
         * Generates a cryptographically random one-time hash in the format:
         * xxxxxx-xxxxxx-xxxxxx-xxxxxx
         * NEVER stored anywhere — shown once and discarded
         * Used by GuardianFlow as the forgot password recovery method
         */
        string GenerateGuardianHash();
    }
}
