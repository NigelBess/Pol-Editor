using System.Text;
using System.Text.RegularExpressions;

namespace PolFileEditor.Models;

/// <summary>
/// Reads and writes the "# Known Networks:" section of a .pol header — the list of
/// friendly network aliases and the hosts defined inside them. This works on the RAW
/// '#'-prefixed header lines (not the stripped display text) so the indented layout is
/// under our control, unlike <see cref="HeaderFormatter"/> which trims every line.
///
/// On disk the block looks like:
/// <code>
/// # Known Networks:
/// #     China: 10.0.30.0/24
/// #         WebServer: 10.0.30.5
/// #         10.0.30.6
/// #     Datacenter: 10.0.40.0/24
/// </code>
/// Network entries carry a CIDR (a '/'); host entries are bare IPv4 addresses, optionally
/// named, indented under their network.
/// </summary>
public static partial class KnownNetworksBlock
{
    private const string Marker = "Known Networks:";
    private const string Indent = "     ";      // network entries: 5 spaces
    private const string HostIndent = "         "; // host entries: 9 spaces

    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$")]
    private static partial Regex Ipv4Regex();

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
        string? currentName = null;
        string? currentCidr = null;
        List<NamedHost> currentHosts = new();

        void Flush()
        {
            if (currentName is not null && currentCidr is not null)
                into.Add(new NamedNetwork(currentName, currentCidr) { Hosts = currentHosts });
        }

        while (end < lines.Count && TryParseEntry(lines[end], out var name, out var value, out var isNetwork))
        {
            if (isNetwork)
            {
                Flush();
                currentName = name;
                currentCidr = value;
                currentHosts = new List<NamedHost>();
            }
            else if (currentName is not null)
            {
                // A host line before any network has nowhere to belong; skip it.
                currentHosts.Add(new NamedHost(name, value));
            }
            end++;
        }
        Flush();

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
            foreach (var h in n.Hosts)
            {
                sb.Append('\n').Append('#').Append(HostIndent);
                if (h.Name.Length > 0)
                    sb.Append(h.Name).Append(": ");
                sb.Append(h.Ip);
            }
        }
        return sb.ToString();
    }

    private static bool IsMarker(string line)
        => string.Equals(Strip(line), Marker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Classifies a raw block line. Networks have a name and a CIDR (contain a '/');
    /// hosts are a bare IPv4 address, optionally prefixed with "name:". Returns false for
    /// anything else (which stops the block scan).
    /// </summary>
    private static bool TryParseEntry(string line, out string name, out string value, out bool isNetwork)
    {
        name = "";
        value = "";
        isNetwork = false;

        var content = Strip(line);
        if (content.Length == 0)
            return false;

        string rest;
        var colon = content.IndexOf(':');
        if (colon > 0)
        {
            name = content[..colon].Trim();
            rest = content[(colon + 1)..].Trim();
        }
        else
        {
            rest = content;
        }

        if (rest.Contains('/'))
        {
            // A network alias: "Name: a.b.c.d/nn".
            if (name.Length == 0)
                return false;
            value = rest;
            isNetwork = true;
            return true;
        }

        // A host: a bare IPv4, optionally named. The IPv4 shape guards against consuming
        // unrelated trailing header comments as hosts.
        if (!Ipv4Regex().IsMatch(rest))
            return false;

        value = rest;
        isNetwork = false;
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
