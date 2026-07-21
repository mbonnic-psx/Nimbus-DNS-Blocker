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
