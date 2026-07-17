using System.Linq;
using Nimbus_Internet_Blocker.Utilities;
using Xunit;

namespace Nimbus.Tests;

public class HostsSectionTests
{
    private static string[] Lines(string s) =>
        s.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

    private static string NewSection(params string[] entryLines) =>
        string.Join("\n", new[] { HostsSection.BeginMarker }
            .Concat(entryLines)
            .Concat(new[] { HostsSection.EndMarker }));

    [Fact]
    public void Splice_NoMarkers_AppendsAfterOriginalContent()
    {
        var original = new[] { "127.0.0.1 localhost", "# some comment" };
        var hostsContent = string.Join("\n", original);
        var sectionLines = new[] { HostsSection.BeginMarker, "0.0.0.0         facebook.com", HostsSection.EndMarker };
        var newSection = string.Join("\n", sectionLines);

        var lines = Lines(HostsSection.Splice(hostsContent, newSection));

        // Original content is untouched and comes first, in order
        Assert.Equal(original[0], lines[0]);
        Assert.Equal(original[1], lines[1]);

        // A blank separator line follows the original content
        Assert.Equal("", lines[2]);

        // The new section follows the separator, in order
        Assert.Equal(HostsSection.BeginMarker, lines[3]);
        Assert.Contains("0.0.0.0         facebook.com", lines);
        Assert.Equal(HostsSection.EndMarker, lines[5]);
    }

    [Fact]
    public void Splice_BothMarkersPresent_ReplacesOnlyTheDelimitedBlock()
    {
        var before = new[] { "127.0.0.1 localhost", "# custom line" };
        var staleBlock = NewSection("0.0.0.0         old-site.com").Split('\n');
        var after = new[] { "192.168.1.1 router.local" };
        var hostsContent = string.Join("\n", before.Concat(staleBlock).Concat(after));

        var newSectionLines = new[] { HostsSection.BeginMarker, "0.0.0.0         facebook.com", HostsSection.EndMarker };
        var newSection = string.Join("\n", newSectionLines);

        var lines = Lines(HostsSection.Splice(hostsContent, newSection));

        var expected = before.Concat(newSectionLines).Concat(after).ToArray();
        Assert.Equal(expected, lines);
        Assert.DoesNotContain(lines, l => l.Contains("old-site.com"));
    }

    [Fact]
    public void Splice_CrlfAndLoneCrInput_MatchLfInputAfterLineNormalization()
    {
        var contentLines = new[]
        {
            "127.0.0.1 localhost",
            HostsSection.BeginMarker,
            "0.0.0.0 old.com",
            HostsSection.EndMarker,
            "8.8.8.8 dns"
        };
        var lf   = string.Join("\n", contentLines);
        var crlf = string.Join("\r\n", contentLines);
        var cr   = string.Join("\r", contentLines);

        var newSection = string.Join("\n", new[] { HostsSection.BeginMarker, "0.0.0.0 new.com", HostsSection.EndMarker });

        var lfResult   = Lines(HostsSection.Splice(lf, newSection));
        var crlfResult = Lines(HostsSection.Splice(crlf, newSection));
        var crResult   = Lines(HostsSection.Splice(cr, newSection));

        Assert.Equal(lfResult, crlfResult);
        Assert.Equal(lfResult, crResult);
    }

    [Fact]
    public void Splice_IndentedMarkers_AreStillRecognizedAfterTrim()
    {
        var hostsContent = string.Join("\n", new[]
        {
            "127.0.0.1 localhost",
            "   " + HostsSection.BeginMarker + "  ",
            "0.0.0.0 old.com",
            "  " + HostsSection.EndMarker,
            "8.8.8.8 dns"
        });

        var newSectionLines = new[] { HostsSection.BeginMarker, "0.0.0.0 new.com", HostsSection.EndMarker };
        var newSection = string.Join("\n", newSectionLines);

        var lines = Lines(HostsSection.Splice(hostsContent, newSection));

        var expected = new[] { "127.0.0.1 localhost" }
            .Concat(newSectionLines)
            .Concat(new[] { "8.8.8.8 dns" })
            .ToArray();
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Splice_DuplicateBeginMarkerBeforeEnd_ReplacesFromFirstBeginThroughEnd()
    {
        var hostsContent = string.Join("\n", new[]
        {
            "127.0.0.1 localhost",
            HostsSection.BeginMarker,
            "0.0.0.0 dup1.com",
            HostsSection.BeginMarker, // duplicate/inner marker — removed along with the block
            "0.0.0.0 dup2.com",
            HostsSection.EndMarker,
            "8.8.8.8 dns"
        });

        var newSectionLines = new[] { HostsSection.BeginMarker, "0.0.0.0 new.com", HostsSection.EndMarker };
        var newSection = string.Join("\n", newSectionLines);

        var lines = Lines(HostsSection.Splice(hostsContent, newSection));

        var expected = new[] { "127.0.0.1 localhost" }
            .Concat(newSectionLines)
            .Concat(new[] { "8.8.8.8 dns" })
            .ToArray();
        Assert.Equal(expected, lines);
        Assert.DoesNotContain(lines, l => l.Contains("dup1.com") || l.Contains("dup2.com"));
    }

    [Fact]
    public void Splice_ReversedMarkers_FallsBackToAppendWithoutLosingAnyOriginalLine()
    {
        // Quirky but deliberately non-destructive: when END appears before BEGIN, the
        // "both markers found" branch never activates (endIdx < beginIdx), so the code
        // falls back to the append path. The invariant that matters is "never delete
        // non-Nimbus lines" — it does not matter that this produces a duplicated block.
        var originalLines = new[]
        {
            "127.0.0.1 localhost",
            HostsSection.EndMarker,
            "8.8.8.8 dns",
            HostsSection.BeginMarker,
            "1.1.1.1 cloudflare"
        };
        var hostsContent = string.Join("\n", originalLines);

        var newSection = string.Join("\n", new[] { HostsSection.BeginMarker, "0.0.0.0 new.com", HostsSection.EndMarker });

        var lines = HostsSection.Splice(hostsContent, newSection);

        foreach (var original in originalLines)
            Assert.Contains(original, lines);
    }

    [Fact]
    public void Splice_EmptyContent_AppendsFullNewSection()
    {
        var sectionLines = new[] { HostsSection.BeginMarker, "0.0.0.0 new.com", HostsSection.EndMarker };
        var newSection = string.Join("\n", sectionLines);

        var lines = Lines(HostsSection.Splice("", newSection));

        foreach (var line in sectionLines)
            Assert.Contains(line, lines);
    }

    [Fact]
    public void Splice_SectionAtEndOfFileWithoutTrailingNewline_IsReplacedCorrectly()
    {
        var hostsContent = string.Join("\n", new[]
        {
            "127.0.0.1 localhost",
            HostsSection.BeginMarker,
            "0.0.0.0 old.com",
            HostsSection.EndMarker
        }); // no trailing newline — the Nimbus block is the very last thing in the file

        var newSectionLines = new[] { HostsSection.BeginMarker, "0.0.0.0 new.com", HostsSection.EndMarker };
        var newSection = string.Join("\n", newSectionLines);

        var lines = Lines(HostsSection.Splice(hostsContent, newSection));

        var expected = new[] { "127.0.0.1 localhost" }.Concat(newSectionLines).ToArray();
        Assert.Equal(expected, lines);
    }

    [Fact]
    public void Splice_AppliedTwiceWithSameSection_IsIdempotent()
    {
        var hostsContent = string.Join("\n", new[] { "127.0.0.1 localhost", "8.8.8.8 dns" });
        var newSection = string.Join("\n", new[] { HostsSection.BeginMarker, "0.0.0.0 facebook.com", HostsSection.EndMarker });

        var once  = HostsSection.Splice(hostsContent, newSection);
        var twice = HostsSection.Splice(once, newSection);

        Assert.Equal(Lines(once), Lines(twice));
    }

    private static string[] TrimTrailingEmpty(string[] lines)
    {
        int end = lines.Length;
        while (end > 0 && lines[end - 1].Length == 0) end--;
        return lines[..end];
    }

    [Fact]
    public void Remove_BlockPresent_DeletesMarkersAndEverythingBetween()
    {
        var before = new[] { "127.0.0.1 localhost", "# comment" };
        var block = new[] { HostsSection.BeginMarker, "0.0.0.0 facebook.com", HostsSection.EndMarker };
        var after = new[] { "8.8.8.8 dns" };
        var hostsContent = string.Join("\n", before.Concat(block).Concat(after));

        var lines = Lines(HostsSection.Remove(hostsContent));

        var expected = before.Concat(after).ToArray();
        Assert.Equal(expected, lines);
        Assert.DoesNotContain(lines, l => l.Contains("facebook.com"));
    }

    [Fact]
    public void Remove_NoMarkers_ReturnsContentUnchanged()
    {
        var hostsContent = string.Join("\n", new[] { "127.0.0.1 localhost", "8.8.8.8 dns" });

        var result = HostsSection.Remove(hostsContent);

        Assert.Equal(hostsContent, result);
    }

    [Fact]
    public void Remove_ReversedMarkers_ReturnsContentUnchanged()
    {
        var hostsContent = string.Join("\n", new[]
        {
            "127.0.0.1 localhost",
            HostsSection.EndMarker,
            "8.8.8.8 dns",
            HostsSection.BeginMarker,
            "1.1.1.1 cloudflare"
        });

        var result = HostsSection.Remove(hostsContent);

        Assert.Equal(hostsContent, result);
    }

    [Fact]
    public void Remove_CrlfInput_MatchesLfInputAfterLineNormalization()
    {
        var contentLines = new[]
        {
            "127.0.0.1 localhost",
            HostsSection.BeginMarker,
            "0.0.0.0 old.com",
            HostsSection.EndMarker,
            "8.8.8.8 dns"
        };
        var lf   = string.Join("\n", contentLines);
        var crlf = string.Join("\r\n", contentLines);

        var lfResult   = Lines(HostsSection.Remove(lf));
        var crlfResult = Lines(HostsSection.Remove(crlf));

        Assert.Equal(lfResult, crlfResult);
    }

    [Fact]
    public void Remove_BlockAtEndOfFileWithoutTrailingNewline_IsRemovedCleanly()
    {
        var hostsContent = string.Join("\n", new[]
        {
            "127.0.0.1 localhost",
            HostsSection.BeginMarker,
            "0.0.0.0 old.com",
            HostsSection.EndMarker
        }); // no trailing newline — the Nimbus block is the very last thing in the file

        var lines = Lines(HostsSection.Remove(hostsContent));

        Assert.Equal(new[] { "127.0.0.1 localhost" }, lines);
    }

    [Fact]
    public void Remove_AfterSplice_RoundTripsBackToOriginalContent()
    {
        var originalLines = new[] { "127.0.0.1 localhost", "8.8.8.8 dns" };
        var original = string.Join("\n", originalLines);
        var section = string.Join("\n", new[] { HostsSection.BeginMarker, "0.0.0.0 facebook.com", HostsSection.EndMarker });

        var applied  = HostsSection.Splice(original, section);
        var restored = HostsSection.Remove(applied);

        var restoredLines = TrimTrailingEmpty(Lines(restored));
        Assert.Equal(originalLines, restoredLines);
    }
}
