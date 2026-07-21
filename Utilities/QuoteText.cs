namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// Pure quote helpers — the date→index pick and the comparison normalization used
/// by the "quote of the day" and by AccountabilityFlow Q5. MAUI-free so the plain
/// net9.0 test project can compile and test it directly.
/// </summary>
public static class QuoteText
{
    /// <summary>
    /// Deterministic index of the quote for a given local date. Same date always
    /// yields the same index; consecutive days step by one (mod count) so the quote
    /// visibly changes at midnight. Returns 0 when count is non-positive.
    /// </summary>
    public static int IndexForDate(DateOnly date, int count)
        => count <= 0 ? 0 : (int)(date.DayNumber % count);

    /// <summary>
    /// Normalizes a quote or a typed answer for equality comparison: trims and
    /// lowercases. Punctuation is significant (not stripped). Null/blank → "".
    /// </summary>
    public static string Normalize(string? text)
        => string.IsNullOrWhiteSpace(text) ? "" : text.Trim().ToLowerInvariant();
}
