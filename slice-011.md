# Slice 011 — Phase 2: Daily quote + AccountabilityFlow Q5 (text-only)

> **You are the executing model (Sonnet 5).** Read this whole file, then make the edits
> below. Follow the repo's `CLAUDE.md` rules exactly. **Do NOT commit, push, or run any
> `git` command** — the human handles all pushes. When done, report what you changed and
> the checklist at the bottom.

---

## 1. Goal

Three linked quote problems (Tech Debt #11's remaining items):

1. **The quote isn't daily.** `QuoteService.CurrentQuote` picks a random quote per app launch
   (`rand.Next`), so "Quote of the Day" changes every time the app opens and can repeat.
2. **The quote list is structurally inconsistent.** Each quote is a single pre-formatted string
   with escaped quotes and the author baked in, but in two different shapes — sometimes the
   author sits in its own quoted segment (`"...text..." - "Winston Churchill"`) and sometimes
   it's inside the same quotes as the text (`"...text... - Anonymous"`). Plus a couple of typos.
3. **AccountabilityFlow Q5 makes the user type the author.** `GetNormalizedQuote()` strips the
   `"` marks from the *whole* `CurrentQuote` (text **and** author) and compares — so to pass, the
   user must transcribe `... - winston churchill`, not just the quote. PLAN.md wants Q5 to
   validate the **quote text only**.

The root cause of #2 and #3 is that text and author are fused into one string. This slice
restructures quotes into `Text` + `Author`, seeds the daily pick by date, cleans the list, and
makes Q5 compare against the text alone. The pure bits (date→index, comparison normalization)
go into a testable `Utilities/` file per the project's established pattern (slices 005/006/008).

**Decisions already made (do not deviate):**

- **Structured quotes.** Introduce `public sealed record Quote(string Text, string Author)` and
  make `QuoteService` hold a `List<Quote>`. No more escaped quotes or embedded `- Author` in the
  data.
- **Date-seeded, deterministic, no `Random`.** The day's index is
  `QuoteText.IndexForDate(DateOnly.FromDateTime(DateTime.Now), count)` — same quote all day,
  same across launches, advances at local midnight. Cache the pick and recompute only when the
  date changes (so a session left open past midnight rolls over correctly).
- **Two public strings, cleanly separated:**
  - `CurrentQuote` → the **display** form with author, e.g. `"…text…" — Author`. The dashboard
    (`Home.razor`) and the Q-flow keep calling this for display; those call sites don't change.
  - `CurrentQuoteText` → the **quote text only**. Used by Q5 for both the on-screen box the user
    reads and the validation, so "type what you see" holds and the author is never typed.
- **Comparison normalization = trim + lowercase only.** Matches the prior rigor (punctuation was
  always significant; only the quote marks were stripped, and those are gone from the data now).
  Do **not** strip internal punctuation or collapse whitespace — keep behaviour predictable.
- **Delete the now-unused `GetQuotes`** (`IReadOnlyList<string>`) — grep confirms no consumer.
- **Remove the tutorial-style comments** in the files you touch (CLAUDE.md standing rule), e.g.
  QuoteService's per-launch narration and AccountabilityFlow's `GetNormalizedQuote` blurb. Keep
  comments that state *invariants* (the early-exit Q3/Q4 rationale, the paste-listener note).
- **No new NuGet, no MAUI in the utility.** `QuoteText.cs` uses only `System` types so
  `Nimbus.Tests` can compile it directly, exactly like `HostValidation`/`HostsSection`/`PasswordHash`.

**Scope discipline:** quotes and Q5 only. Don't touch the other AccountabilityFlow questions,
the timer, GuardianFlow, the hosts/config services, CSS, or DI. The broader "delete tutorial
comments / stale personal paths" sweep across unrelated files is **not** this slice (and slice
009 already found `Services/` has no stale personal paths).

---

## 2. Before you start

Read in full: `Services/QuoteService.cs`, `Components/Shared/AccountabilityFlow.razor`
(especially the `_questions` list ~129–151, `OnNextClickedAsync` ~207–258, and
`GetNormalizedQuote` ~287–291), `Components/Pages/Home.razor` (line ~22, the display), and
`Nimbus.Tests/Nimbus.Tests.csproj` + `Nimbus.Tests/HostValidationTests.cs` for the test style.
Consumers of `QuoteService`, confirmed by grep: `Home.razor` (`CurrentQuote`, display) and
`AccountabilityFlow.razor` (`CurrentQuote` display + `GetNormalizedQuote` validation). `GetQuotes`
has no consumers.

---

## 3. Edits

### Edit A — new file `Utilities/QuoteText.cs`

```csharp
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
```

### Edit B — rewrite `Services/QuoteService.cs`

Replace the whole class body. Keep it a singleton-friendly plain class (still registered as
`AddSingleton<QuoteService>()` — no interface needed; it touches no file system or OS).

```csharp
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
```

Notes for the executing model:
- The cleaned quote list above is the target — it fixes the grammar in the "seeds" quote
  (`planted` / `shape`), replaces the trailing `...` in the Genesis quote with clean
  punctuation, and normalizes attribution. Use it verbatim.
- The `—` in `CurrentQuote` is an em dash (U+2014); the file is UTF-8, so it's fine. If your
  editor can't emit it cleanly, ` - ` (spaced hyphen) is an acceptable fallback — say so in your
  report.
- `[ ... ]` collection expressions and `new(...)` target-typed records are already used
  elsewhere in the repo (e.g. AccountabilityFlow's `_questions`), so they're in-style.

### Edit C — `Components/Shared/AccountabilityFlow.razor`

**C1. Q5 box shows text only (line ~30).** So what the user reads equals what they must type:

```razor
<div class="af-quote-text">@QuoteService.CurrentQuoteText</div>
```

**C2. Validation uses the text (the `GetNormalizedQuote` helper ~287–291).** Rewrite it to
normalize the text-only property via the shared helper, and use the same helper on the typed
answer so both sides normalize identically:

```csharp
// Q5 accepts the quote TEXT only (no author) — see slice 011.
private string GetNormalizedQuote()
    => QuoteText.Normalize(QuoteService.CurrentQuoteText);
```

In `OnNextClickedAsync`, change the answer normalization (line ~211) so the last-question
comparison is apples-to-apples:

```csharp
var normalized = QuoteText.Normalize(_typedAnswer);
```

(That keeps Q1–Q4 working — they already compare `normalized` against lowercase accepted
answers; `Normalize` is trim + lowercase, same as the old `.Trim().ToLowerInvariant()`.)

Add `@using Nimbus_Internet_Blocker.Utilities` at the top of the file if it isn't already
imported globally (check `_Imports.razor`; add the local `@using` only if needed).

**C3. Hint + comments.** Update the Q5 hint (line ~278) to make "text only" explicit, e.g.
`"Type the quote above exactly (no author) to continue"`. Update the stale comments: line ~127
(`validated dynamically against QuoteService.CurrentQuote (stripped of quotes)`) and the
`GetNormalizedQuote` doc blurb — they should describe comparing against `CurrentQuoteText`, not
quote-stripping. Don't touch the timer, paste-listener, or Q3/Q4 early-exit logic/comments.

### Edit D — `Components/Pages/Home.razor`

**No code change** — it keeps rendering `@QuoteService.CurrentQuote` (the display form with
author). Just confirm it still compiles against the new API (it does). Do not restyle it.

### Edit E — `Nimbus.Tests/Nimbus.Tests.csproj`

Add the fourth `<Compile Include>`:

```xml
<Compile Include="..\Utilities\QuoteText.cs" Link="src\QuoteText.cs" />
```

### Edit F — new file `Nimbus.Tests/QuoteTextTests.cs`

```csharp
using System;
using Nimbus_Internet_Blocker.Utilities;
using Xunit;

namespace Nimbus.Tests;

public class QuoteTextTests
{
    [Fact]
    public void IndexForDate_IsDeterministicForSameDate()
    {
        var date = new DateOnly(2026, 7, 21);
        Assert.Equal(QuoteText.IndexForDate(date, 10), QuoteText.IndexForDate(date, 10));
    }

    [Fact]
    public void IndexForDate_StaysInRange()
    {
        var date = new DateOnly(2026, 1, 1);
        for (int count = 1; count <= 12; count++)
        {
            var idx = QuoteText.IndexForDate(date, count);
            Assert.InRange(idx, 0, count - 1);
        }
    }

    [Fact]
    public void IndexForDate_ChangesFromOneDayToTheNext()
    {
        var day1 = new DateOnly(2026, 7, 21);
        var day2 = day1.AddDays(1);
        Assert.NotEqual(QuoteText.IndexForDate(day1, 10), QuoteText.IndexForDate(day2, 10));
    }

    [Fact]
    public void IndexForDate_CyclesOverTheList()
    {
        // Across `count` consecutive days every index appears exactly once.
        var start = new DateOnly(2026, 7, 21);
        var seen = new bool[10];
        for (int i = 0; i < 10; i++)
            seen[QuoteText.IndexForDate(start.AddDays(i), 10)] = true;
        Assert.DoesNotContain(false, seen);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void IndexForDate_NonPositiveCount_ReturnsZero(int count)
    {
        Assert.Equal(0, QuoteText.IndexForDate(new DateOnly(2026, 7, 21), count));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("  Keep Going.  ", "keep going.")]
    [InlineData("It Does Not Matter", "it does not matter")]
    public void Normalize_TrimsAndLowercases(string? input, string expected)
    {
        Assert.Equal(expected, QuoteText.Normalize(input));
    }
}
```

---

## 4. Docs to update

- **`CLAUDE.md`**
  - **Project Structure**: `QuoteService.cs` comment → "Deterministic daily quote seeded by
    date (DONE)"; add `Utilities/QuoteText.cs` (DONE) and `Nimbus.Tests/QuoteTextTests.cs`;
    update the `Nimbus.Tests/` note to say it now compiles four utility files.
  - **Tech Debt #11**: strike the resolved items — quote is now date-seeded (not per-launch
    random), quote-list typos/inconsistent quoting fixed, and AccountabilityFlow Q5 validates
    quote text only. Note what (if anything) is left in #11 (the broad tutorial-comment sweep,
    if you consider it still open — but `rain.js`/`JsonLoad.cs`/`exclude` were slice 009).
  - **Tech Debt #10**: extend the covered-pure-functions list to include
    `QuoteText.IndexForDate`/`Normalize`.
  - **Password/flow section** ("How It Actually Works"): update the Q5 description — it requires
    typing the daily quote **text** (author excluded), paste still blocked.
  - **What's Done**: add a bullet — daily date-seeded quote + text-only Q5, both backed by a
    tested pure utility.
- **`PLAN.md`** → Phase 2: check the box **"Quote of the day = seeded by date …; fix
  typos/inconsistent quoting …; AccountabilityFlow Q5 validates quote text only (not author
  line)."** If the tutorial-comment/personal-path junk item is now effectively done, note it;
  otherwise leave that one line as the last open Phase 2 item.

---

## 5. Verify

- Run `dotnet test Nimbus.Tests/Nimbus.Tests.csproj` — all prior tests plus the new
  `QuoteTextTests` must pass. This is pure `System` code and builds/tests on Linux/WSL.
- The MAUI Windows head only builds on Windows; if it can't build here, say so and verify by
  inspection:
  - `QuoteService` exposes `CurrentQuote` and `CurrentQuoteText`, no `Random`, no `GetQuotes`;
    the `Quote` record and `_quotes` list are well-formed (10 entries, balanced quotes/parens).
  - `AccountabilityFlow` compiles conceptually: `GetNormalizedQuote` and the answer both go
    through `QuoteText.Normalize`; the Q5 box binds `CurrentQuoteText`; `@using` for
    `Utilities` resolves (globally or locally).
  - `Home.razor` still binds `CurrentQuote`.
  - `grep -rn "GetQuotes\|rand\.Next"` (excluding obj/bin) returns nothing.
- **Do NOT** run git, commit, or push.

### Manual check for the human (Windows, include in your report)

1. Dashboard shows a quote **with** author. Close and reopen the app several times the same day
   → the **same** quote appears every time (confirms daily, not per-launch).
2. (Optional, proves the date seed) Temporarily change the system date forward a day and relaunch
   → a **different** quote appears; set the clock back afterward.
3. Accountability mode → trigger the unlock flow → reach Q5. The box shows the quote **text only
   (no author)**. Typing just the text passes; typing the author too now **fails**. Paste is
   still blocked.

---

## 6. When finished

Summarize for the human: files created/changed (one line each), the `dotnet test` result,
whether the Windows head was buildable here (say so if not), the manual checklist above, and
whether you used the em dash or the ` - ` fallback in `CurrentQuote`. Confirm in one line that
the dashboard display is unchanged in shape (quote + author) and only Q5's requirement changed
(text only).
