namespace PolFileEditor.Models;

/// <summary>A selectable IP protocol option: what the user sees plus the stored value.</summary>
public sealed record ProtocolOption(string Display, string Value)
{
    public override string ToString() => Display;
}

/// <summary>
/// The list of IP protocols offered in the protocol combo box. Every value 0-255 is
/// included (so any protocol number is selectable, per the project spec), with IANA
/// names for the well-known ones and a bare number for the rest. Index 0 is "Unused".
/// </summary>
public static class ProtocolCatalog
{
    // Well-known IANA IP protocol numbers (https://www.iana.org/assignments/protocol-numbers).
    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [0] = "HOPOPT", [1] = "ICMP", [2] = "IGMP", [3] = "GGP", [4] = "IPv4",
        [5] = "ST", [6] = "TCP", [8] = "EGP", [9] = "IGP", [17] = "UDP",
        [27] = "RDP", [33] = "DCCP", [41] = "IPv6", [43] = "IPv6-Route",
        [44] = "IPv6-Frag", [46] = "RSVP", [47] = "GRE", [50] = "ESP", [51] = "AH",
        [58] = "IPv6-ICMP", [59] = "IPv6-NoNxt", [60] = "IPv6-Opts", [88] = "EIGRP",
        [89] = "OSPF", [94] = "IPIP", [103] = "PIM", [108] = "IPComp", [112] = "VRRP",
        [115] = "L2TP", [124] = "IS-IS", [132] = "SCTP", [133] = "FC",
        [136] = "UDPLite", [137] = "MPLS-in-IP",
    };

    /// <summary>The option representing an unused protocol field (serializes to "-").</summary>
    public static readonly ProtocolOption Unused = new("— Unused —", "");

    public static IReadOnlyList<ProtocolOption> Options { get; } = Build();

    private static IReadOnlyList<ProtocolOption> Build()
    {
        var list = new List<ProtocolOption>(258) { Unused };
        for (var i = 0; i <= 255; i++)
        {
            var display = Names.TryGetValue(i, out var name) ? $"{name} ({i})" : $"Protocol {i}";
            list.Add(new ProtocolOption(display, i.ToString()));
        }
        return list;
    }

    /// <summary>Maps a stored protocol string ("6", "-", "") to its option (falls back to Unused).</summary>
    public static ProtocolOption FromValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            return Unused;

        var trimmed = value.Trim();
        return Options.FirstOrDefault(o => o.Value == trimmed) ?? Unused;
    }
}
