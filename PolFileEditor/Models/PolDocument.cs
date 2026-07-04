namespace PolFileEditor.Models;

/// <summary>
/// A single firewall rule. All values are strings; an empty string means the field
/// is unused and serializes to "-". RuleNumber is assigned by the app (auto-numbered).
/// </summary>
public sealed class PolRule
{
    public string Action { get; set; } = "Block";
    public string MacSource { get; set; } = "";
    public string MacDest { get; set; } = "";
    public string IpSource { get; set; } = "";
    public string IpDest { get; set; } = "";
    public string IpProtocol { get; set; } = "";
    public string PortSource { get; set; } = "";
    public string PortDest { get; set; } = "";
    public string Comment { get; set; } = "";
}

/// <summary>
/// A single host inside a known network: an optional friendly name and a bare IPv4 address
/// (no subnet — the host belongs to its network's subnet). Persisted indented under its
/// network in the "# Known Networks:" block.
/// </summary>
public sealed record NamedHost(string Name, string Ip);

/// <summary>
/// A user-defined alias mapping a friendly name to a network in CIDR notation
/// (e.g. "China" -> "10.0.30.0/24"), optionally with named hosts inside it.
/// Persisted in the file's "# Known Networks:" block.
/// </summary>
public sealed record NamedNetwork(string Name, string Cidr)
{
    /// <summary>Specific hosts defined inside this network.</summary>
    public IReadOnlyList<NamedHost> Hosts { get; init; } = Array.Empty<NamedHost>();

    // The default record equality compares Hosts by reference; make it element-wise so two
    // networks with equal hosts (from different collection instances) compare equal.
    public bool Equals(NamedNetwork? other)
        => other is not null
           && Name == other.Name
           && Cidr == other.Cidr
           && Hosts.SequenceEqual(other.Hosts);

    public override int GetHashCode() => HashCode.Combine(Name, Cidr, Hosts.Count);
}

/// <summary>A group of rules, corresponding to a "# Task N" section in the .pol file.</summary>
public sealed class PolTask
{
    public int Number { get; set; }
    public List<PolRule> Rules { get; } = new();
}

/// <summary>
/// A parsed .pol document: the leading documentation/comment block (preserved verbatim)
/// plus the ordered list of tasks.
/// </summary>
public sealed class PolDocument
{
    public string HeaderText { get; set; } = "";

    /// <summary>Named network aliases, stored in the "# Known Networks:" comment block.</summary>
    public List<NamedNetwork> KnownNetworks { get; } = new();

    public List<PolTask> Tasks { get; } = new();

    /// <summary>
    /// The template header seeded into a new (File &gt; New) document. Stored WITHOUT '#'
    /// markers — the serializer adds them. This is the plain text the user edits.
    /// </summary>
    public const string DefaultHeader =
        "SDN Firewall Policy (.pol)\n" +
        "\n" +
        "Rule Format:\n" +
        "RuleNumber,Action,Source MAC,Destination MAC,Source IP,Destination IP,Protocol,Source Port,Destination Port,Comment/Note\n" +
        "Action = Block or Allow (Allow rules take precedence over Block rules)\n" +
        "Source / Destination MAC address in the form xx:xx:xx:xx:xx:xx\n" +
        "Source / Destination IP address in CIDR notation xxx.xxx.xxx.xxx/xx (must be a valid network address)\n" +
        "Protocol = integer IP protocol number per IANA (0-254, e.g. 1=ICMP, 6=TCP, 17=UDP)\n" +
        "Source / Destination Port = application port number for TCP/UDP\n" +
        "Any unused field should be a single '-' character.";
}
