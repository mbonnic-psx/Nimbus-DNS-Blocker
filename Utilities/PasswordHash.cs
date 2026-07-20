using System.Security.Cryptography;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// PBKDF2 password hashing using the framework's own primitives — the single
/// source of truth for both the Guardian password and the recovery code.
/// Replaces the ASP.NET Identity <c>PasswordHasher&lt;T&gt;</c> so the two
/// EOL/legacy Identity packages can be dropped (Tech Debt #6).
///
/// Stored format (one self-describing string; no separate salt field needed):
///     v1.{iterations}.{base64(salt)}.{base64(subkey)}
/// </summary>
public static class PasswordHash
{
    private const string Version = "v1";
    private const int    Iterations = 600_000;   // PBKDF2-HMAC-SHA256, OWASP-range
    private const int    SaltBytes  = 16;
    private const int    KeyBytes   = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Hashes a password with a fresh random salt and returns the self-describing
    /// <c>v1.iterations.salt.subkey</c> string to persist.
    /// </summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var subkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeyBytes);

        return string.Join('.',
            Version,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(subkey));
    }

    /// <summary>
    /// Verifies an attempt against a stored hash. Total and constant-time:
    /// returns false for any null/empty/malformed stored hash (including the old
    /// ASP.NET Identity format — those users must re-set their password) and
    /// never throws.
    /// </summary>
    public static bool Verify(string? attempt, string? storedHash)
    {
        if (string.IsNullOrEmpty(attempt) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != Version) return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0) return false;

        byte[] salt, expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expectedSubkey.Length == 0) return false;

        var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(
            attempt, salt, iterations, Algorithm, expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
