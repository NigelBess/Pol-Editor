using System.Text;

namespace PolFileEditor.Models;

/// <summary>
/// Turns a <see cref="PolRule"/> into a plain-English sentence, e.g.
/// "Allow TCP traffic from China to DC on destination port 443." Unused fields are
/// omitted, and IPs are named via an optional <see cref="NetworkNameResolver"/>.
/// </summary>
public static class RuleDescriber
{
    public static string Describe(PolRule rule, NetworkNameResolver? resolver = null)
    {
        var verb = string.Equals(rule.Action?.Trim(), "Allow", StringComparison.OrdinalIgnoreCase)
            ? "Allow"
            : "Block";

        var protocol = ProtocolWord(rule.IpProtocol);
        var protocolPrefix = protocol is null ? "" : protocol + " ";

        var from = Endpoint(rule.MacSource, rule.IpSource, rule.PortSource, "source", resolver);
        var to = Endpoint(rule.MacDest, rule.IpDest, rule.PortDest, "destination", resolver);

        var sb = new StringBuilder();
        sb.Append(verb).Append(' ');

        if (from is null && to is null)
            return sb.Append("all ").Append(protocolPrefix).Append("traffic.").ToString();

        // With both endpoints, read it as one flow ("from X to Y"). With only one, keep the
        // outgoing/incoming label so the single endpoint's direction stays clear.
        if (from is not null && to is not null)
            sb.Append(protocolPrefix).Append("traffic from ").Append(from).Append(" to ").Append(to);
        else if (from is not null)
            sb.Append("outgoing ").Append(protocolPrefix).Append("traffic from ").Append(from);
        else
            sb.Append("incoming ").Append(protocolPrefix).Append("traffic to ").Append(to);

        return sb.Append('.').ToString();
    }

    /// <summary>Renders one endpoint (MAC/IP location plus an "on ... port" suffix), or null
    /// when every field is unused.</summary>
    private static string? Endpoint(string mac, string ip, string port, string direction,
        NetworkNameResolver? resolver)
    {
        var location = new List<string>();
        if (!Validators.IsUnused(mac))
            location.Add($"MAC {mac.Trim()}");
        if (!Validators.IsUnused(ip))
            location.Add(resolver?.Resolve(ip) ?? $"IP {ip.Trim()}");

        var hasPort = !Validators.IsUnused(port);
        if (location.Count == 0 && !hasPort)
            return null;

        var portClause = $"{direction} port {port.Trim()}";
        if (location.Count == 0)
            return $"any IP address using {portClause}";

        var loc = string.Join(" and ", location);
        return hasPort ? $"{loc} on {portClause}" : loc;
    }

    /// <summary>Protocol number -> friendly word ("6" -> "TCP", "99" -> "Protocol 99"),
    /// or null when unused.</summary>
    private static string? ProtocolWord(string protocol)
    {
        if (Validators.IsUnused(protocol))
            return null;

        var display = ProtocolCatalog.FromValue(protocol).Display;
        var paren = display.IndexOf(" (", StringComparison.Ordinal);
        return paren > 0 ? display[..paren] : display;
    }
}
