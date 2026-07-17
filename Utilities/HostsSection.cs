using System.Text;

namespace Nimbus_Internet_Blocker.Utilities;

/// <summary>
/// Pure logic for the Nimbus-managed block inside the Windows hosts file.
/// Lines outside the delimited section are never modified.
/// </summary>
public static class HostsSection
{
    public const string BeginMarker = "# --- Nimbus-managed section BEGIN ---";
    public const string EndMarker   = "# --- Nimbus-managed section END ---";

    /// <summary>
    /// Replaces the existing Nimbus-managed section in <paramref name="hostsContent"/>
    /// with <paramref name="newSection"/>. When no complete section is found
    /// (first run, or damaged markers), the new section is appended after the
    /// current content instead — never destructively.
    /// </summary>
    public static string Splice(string hostsContent, string newSection)
    {
        // Normalise to \n for index-based line processing
        var lines = hostsContent
            .Replace("\r\n", "\n")
            .Replace("\r",   "\n")
            .Split('\n');

        int beginIdx = -1;
        int endIdx   = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (beginIdx == -1 && trimmed == BeginMarker) { beginIdx = i; continue; }
            if (endIdx   == -1 && trimmed == EndMarker)   { endIdx   = i; }
        }

        // Normalise the incoming section to individual lines as well
        var sectionLines = newSection
            .Replace("\r\n", "\n")
            .Replace("\r",   "\n")
            .Split('\n');

        if (beginIdx >= 0 && endIdx >= 0 && endIdx >= beginIdx)
        {
            // Both markers found — splice in the new block
            var parts = new List<string>(lines.Length);
            parts.AddRange(lines[..beginIdx]);      // everything before BEGIN
            parts.AddRange(sectionLines);            // new Nimbus block (includes markers)
            parts.AddRange(lines[(endIdx + 1)..]);  // everything after END
            return string.Join(Environment.NewLine, parts);
        }

        // No existing section — append after current content with a blank separator
        var sb = new StringBuilder(hostsContent.TrimEnd());
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine(newSection);
        return sb.ToString();
    }
}
