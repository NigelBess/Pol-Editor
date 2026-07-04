using System.Text;

namespace PolFileEditor.Models;

/// <summary>
/// Reads and writes the "# Known Networks:" section of a .pol header — the list of
/// friendly network aliases. This works on the RAW '#'-prefixed header lines (not the
/// stripped display text) so the indented layout is under our control, unlike
/// <see cref="HeaderFormatter"/> which trims every line.
///
/// On disk the block looks like:
/// <code>
/// # Known Networks:
/// #     China: 10.0.30.0/24
/// #     Datacenter: 10.0.40.0/24
/// </code>
/// </summary>
public static class KnownNetworksBlock
{
    private const string Marker = "Known Networks:";
    private const string Indent = "     ";

    /// <summary>
    /// Pulls the known-network entries out of a raw header block (found wherever the
    /// marker appears) into <paramref name="into"/>, and returns the header with those
    /// lines removed. If there is no block, returns <paramref name="rawHeader"/> unchanged.
    /// </summary>
    public static string Extract(string rawHeader, List<NamedNetwork> into)
    {
        var lines = rawHeader.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

        var markerIndex = lines.FindIndex(IsMarker);
        if (markerIndex < 0)
            return rawHeader;

        var end = markerIndex + 1;
        while (end < lines.Count && TryParseEntry(lines[end], out var net))
        {
            into.Add(net);
            end++;
        }

        lines.RemoveRange(markerIndex, end - markerIndex);

        // Collapse a doubled blank line left behind where the block used to sit.
        if (markerIndex > 0 && markerIndex < lines.Count
            && string.IsNullOrWhiteSpace(lines[markerIndex])
            && string.IsNullOrWhiteSpace(lines[markerIndex - 1]))
        {
            lines.RemoveAt(markerIndex);
        }

        return string.Join("\n", lines).Trim('\n');
    }

    /// <summary>
    /// Renders the entries as a '#'-prefixed comment block, or an empty string when
    /// there are no known networks.
    /// </summary>
    public static string ToComments(IReadOnlyList<NamedNetwork> nets)
    {
        if (nets.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.Append("# ").Append(Marker);
        foreach (var n in nets)
        {
            sb.Append('\n').Append('#').Append(Indent)
              .Append(n.Name).Append(": ").Append(n.Cidr);
        }
        return sb.ToString();
    }

    private static bool IsMarker(string line)
        => string.Equals(Strip(line), Marker, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseEntry(string line, out NamedNetwork net)
    {
        net = new NamedNetwork("", "");

        var content = Strip(line);
        var colon = content.IndexOf(':');
        if (colon <= 0)
            return false;

        var name = content[..colon].Trim();
        var cidr = content[(colon + 1)..].Trim();
        if (name.Length == 0 || cidr.Length == 0)
            return false;

        net = new NamedNetwork(name, cidr);
        return true;
    }

    // Raw comment line -> its content with the leading '#' and surrounding space removed.
    private static string Strip(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#'))
            trimmed = trimmed[1..];
        return trimmed.Trim();
    }
}
