using Nimbus_Internet_Blocker.Utilities;

namespace Nimbus_Internet_Blocker.Services;

/// <summary>One quote and its attribution.</summary>
public sealed record Quote(string Text, string Author);

/// <summary>
/// Serves the "quote of the day": a deterministic pick seeded by the local date,
/// stable across app launches within a day (see Utilities/QuoteText.IndexForDate).
/// </summary>
public sealed class QuoteService
{
    private readonly List<Quote> _quotes =
    [
        new("If you are going through hell, keep going.", "Winston Churchill"),
        new("The fastest way to succeed is slowly.", "Anonymous"),
        new("You find out by doing, not thinking.", "Anonymous"),
        new("If you are lost in the forest, keep walking.", "Anonymous"),
        new("It does not matter how slowly you go, as long as you do not stop.", "Confucius"),
        new("A person who thinks all the time has nothing to think about except thoughts.", "Alan Watts"),
        new("The seeds I planted yesterday shape the plant that grows today.", "Anonymous"),
        new("People may spend their whole lives climbing the ladder of success, only to find, once they reach the top, that the ladder is leaning against the wrong wall.", "Thomas Merton"),
        new("If you feel like you are losing everything, remember: trees lose their leaves every year, yet they stand tall and wait for better days to come.", "Anonymous"),
        new("It is not good that man should be alone. We were made for relationships, not just with God, but with each other.", "Genesis 2:18"),
    ];

    private DateOnly _cachedDate;
    private int      _cachedIndex;

    private Quote Current
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (today != _cachedDate)
            {
                _cachedIndex = QuoteText.IndexForDate(today, _quotes.Count);
                _cachedDate  = today;
            }
            return _quotes[_cachedIndex];
        }
    }

    /// <summary>Display form with attribution: <c>"text" — Author</c>.</summary>
    public string CurrentQuote => $"\"{Current.Text}\" — {Current.Author}";

    /// <summary>Quote text only — what the user reads and types in AccountabilityFlow Q5.</summary>
    public string CurrentQuoteText => Current.Text;
}
