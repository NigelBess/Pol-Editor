namespace PolFileEditor.Models;

/// <summary>
/// Converts between the on-disk header comment block (lines prefixed with '#') and the
/// plain text the user edits (no '#'). The app owns the '#' markers so the user cannot
/// accidentally turn a header line into a functional rule.
/// </summary>
public static class HeaderFormatter
{
    /// <summary>On-disk '#'-prefixed comment block -> plain editable text.</summary>
    public static string ToDisplay(string raw)
    {
        var lines = Split(raw).Select(StripCommentMarker);
        return string.Join("\n", lines).Trim('\n');
    }

    /// <summary>Plain editable text -> '#'-prefixed comment block for saving.</summary>
    public static string ToComments(string display)
    {
        var lines = Split(display)
            .Select(line => string.IsNullOrWhiteSpace(line) ? "" : "# " + line.Trim());
        return string.Join("\n", lines).Trim('\n');
    }

    private static string StripCommentMarker(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#'))
            trimmed = trimmed[1..];
        if (trimmed.StartsWith(' '))
            trimmed = trimmed[1..];
        return trimmed;
    }

    private static string[] Split(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
}
