using System.Globalization;
using System.Text.RegularExpressions;

namespace PolFileEditor.Models;

/// <summary>
/// Resolves a rule's CIDR value to a friendly name using the current
/// <see cref="NamedNetwork"/> aliases. Used by <see cref="RuleDescriber"/> so sentences
/// read "China" / "China Host 1" instead of bare addresses.
/// </summary>
public sealed partial class NetworkNameResolver
{
    [GeneratedRegex(@"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})/(\d{1,2})$")]
    private static partial Regex CidrRegex();

    /// <summary>The alias list to resolve against; replaced whenever the user edits it.</summary>
    public IReadOnlyList<NamedNetwork> Networks { get; set; } = Array.Empty<NamedNetwork>();

    /// <summary>
    /// Returns the friendly name for <paramref name="cidr"/>, or null if it matches no
    /// alias (or is unparseable). Matching rules, in priority order:
    /// exact network -> "China"; a /32 inside a network -> the defined host's name
    /// "China / WebServer" if one matches, otherwise the plain IP "10.0.30.5"; a smaller
    /// block inside a network -> "part of China (a.b.c.d/nn)".
    /// </summary>
    public string? Resolve(string? cidr)
    {
        if (!TryParse(cidr, out var address, out var prefix))
            return null;

        foreach (var net in Networks)
        {
            if (!TryParse(net.Cidr, out var baseAddress, out var netPrefix))
                continue;

            var mask = MaskFor(netPrefix);
            var networkBase = baseAddress & mask;

            // The candidate must sit inside this network.
            if ((address & mask) != networkBase)
                continue;

            if (prefix == netPrefix && address == networkBase)
                return net.Name;                                    // the whole network

            if (prefix == 32)
            {
                // Look up a defined host at this address; use its name if it has one,
                // otherwise just show the address (no invented "Host N" numbering).
                var host = net.Hosts.FirstOrDefault(
                    h => TryParse($"{h.Ip}/32", out var hostAddr, out _) && hostAddr == address);
                return host is { Name.Length: > 0 }
                    ? $"{net.Name} / {host.Name}"
                    : FormatIp(address);
            }

            if (prefix > netPrefix)
                return $"part of {net.Name} ({cidr!.Trim()})";      // a contained sub-range
        }

        return null;
    }

    private static string FormatIp(uint address)
        => $"{(address >> 24) & 0xFF}.{(address >> 16) & 0xFF}.{(address >> 8) & 0xFF}.{address & 0xFF}";

    private static bool TryParse(string? cidr, out uint address, out int prefix)
    {
        address = 0;
        prefix = 0;
        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        var match = CidrRegex().Match(cidr.Trim());
        if (!match.Success)
            return false;

        uint result = 0;
        for (var i = 0; i < 4; i++)
        {
            var octet = uint.Parse(match.Groups[i + 1].Value, CultureInfo.InvariantCulture);
            if (octet > 255)
                return false;
            result = (result << 8) | octet;
        }

        prefix = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
        if (prefix > 32)
            return false;

        address = result;
        return true;
    }

    private static uint MaskFor(int prefix)
        => prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
}
