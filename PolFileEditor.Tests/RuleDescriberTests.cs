using PolFileEditor.Models;
using Xunit;

namespace PolFileEditor.Tests;

public class RuleDescriberTests
{
    // Fields use "-" for unused. Params: action, macSrc, macDst, ipSrc, ipDst,
    // proto, portSrc, portDst, useResolver, expected sentence.
    [Theory]
    // ---- Resolver present (China -> 10.0.30.0/24, DC -> 10.0.40.0/24) ----
    [InlineData("Block", "-", "-", "-", "-", "-", "-", "-", true, "Block all traffic.")]
    [InlineData("Allow", "-", "-", "-", "-", "-", "-", "-", true, "Allow all traffic.")]
    [InlineData("Block", "-", "-", "-", "-", "1", "-", "-", true, "Block all ICMP traffic.")]
    [InlineData("Allow", "-", "-", "10.0.30.0/24", "10.0.40.0/24", "6", "-", "443", true,
        "Allow outgoing TCP traffic from China and incoming TCP traffic to DC on destination port 443.")]
    [InlineData("Allow", "-", "-", "10.0.30.1/32", "-", "6", "-", "-", true,
        "Allow outgoing TCP traffic from China Host 1.")]
    [InlineData("Block", "-", "-", "-", "10.0.30.5/32", "17", "-", "53", true,
        "Block incoming UDP traffic to China Host 5 on destination port 53.")]
    [InlineData("Allow", "-", "-", "-", "10.0.30.128/25", "6", "-", "80", true,
        "Allow incoming TCP traffic to part of China (10.0.30.128/25) on destination port 80.")]
    [InlineData("Block", "-", "-", "10.0.99.0/24", "-", "-", "-", "-", true,
        "Block outgoing traffic from IP 10.0.99.0/24.")]
    // Port on one side but no MAC/IP -> "any IP address using ... port".
    [InlineData("Block", "-", "-", "10.0.30.1/32", "-", "17", "-", "53", true,
        "Block outgoing UDP traffic from China Host 1 and incoming UDP traffic to any IP address using destination port 53.")]
    [InlineData("Allow", "-", "-", "-", "-", "-", "1024", "-", false,
        "Allow outgoing traffic from any IP address using source port 1024.")]
    // ---- No resolver (bare IP / MAC / port wording) ----
    [InlineData("Allow", "-", "-", "-", "10.0.2.0/24", "6", "-", "80", false,
        "Allow incoming TCP traffic to IP 10.0.2.0/24 on destination port 80.")]
    [InlineData("Block", "-", "-", "10.0.1.0/24", "-", "17", "53", "-", false,
        "Block outgoing UDP traffic from IP 10.0.1.0/24 on source port 53.")]
    [InlineData("Allow", "-", "-", "10.0.1.0/24", "10.0.2.0/24", "6", "1024", "443", false,
        "Allow outgoing TCP traffic from IP 10.0.1.0/24 on source port 1024 and incoming TCP traffic to IP 10.0.2.0/24 on destination port 443.")]
    [InlineData("Allow", "aa:bb:cc:dd:ee:ff", "-", "-", "-", "-", "-", "-", false,
        "Allow outgoing traffic from MAC aa:bb:cc:dd:ee:ff.")]
    [InlineData("Block", "-", "11:22:33:44:55:66", "10.0.1.0/24", "-", "-", "-", "-", false,
        "Block outgoing traffic from IP 10.0.1.0/24 and incoming traffic to MAC 11:22:33:44:55:66.")]
    public void Describe_produces_expected_sentence(
        string action, string macSrc, string macDst, string ipSrc, string ipDst,
        string proto, string portSrc, string portDst, bool useResolver, string expected)
    {
        var rule = new PolRule
        {
            Action = action,
            MacSource = macSrc,
            MacDest = macDst,
            IpSource = ipSrc,
            IpDest = ipDst,
            IpProtocol = proto,
            PortSource = portSrc,
            PortDest = portDst,
        };

        var resolver = useResolver ? SampleResolver() : null;

        Assert.Equal(expected, RuleDescriber.Describe(rule, resolver));
    }

    [Fact]
    public void Describe_without_resolver_falls_back_to_bare_ip()
    {
        var rule = new PolRule { Action = "Allow", IpDest = "10.0.30.0/24" };
        Assert.Equal("Allow incoming traffic to IP 10.0.30.0/24.", RuleDescriber.Describe(rule));
    }

    private static NetworkNameResolver SampleResolver() => new()
    {
        Networks = new[]
        {
            new NamedNetwork("China", "10.0.30.0/24"),
            new NamedNetwork("DC", "10.0.40.0/24"),
        },
    };
}
