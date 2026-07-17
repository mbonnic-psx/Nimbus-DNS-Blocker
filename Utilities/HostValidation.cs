using System.Text.RegularExpressions;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// Shared host/domain normalization — the single source of truth used by both
/// config services (previously duplicated privately in each).
/// </summary>
public static class HostValidation
{
    /// <summary>
    /// Normalizes user input to a bare lowercase host: strips scheme, path,
    /// port, surrounding whitespace, and trailing dots. Returns "" for
    /// null/blank input.
    /// </summary>
    public static string NormalizeHost(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var s = input.Trim();

        s = Regex.Replace(s, @"^\s*https?://", "", RegexOptions.IgnoreCase);

        var slash = s.IndexOf('/');
        if (slash >= 0) s = s[..slash];

        var colon = s.IndexOf(':');
        if (colon >= 0) s = s[..colon];

        return s.Trim().TrimEnd('.').ToLowerInvariant();
    }
}
