using PolFileEditor.Models;
using Xunit;

namespace PolFileEditor.Tests;

public class NetworkNameResolverTests
{
    private static NetworkNameResolver Resolver() => new()
    {
        Networks = new[]
        {
            new NamedNetwork("China", "10.0.30.0/24"),
            new NamedNetwork("DC", "10.0.40.0/24"),
        },
    };

    [Theory]
    [InlineData("10.0.30.0/24", "China")]        // exact whole network
    [InlineData("10.0.40.0/24", "DC")]
    [InlineData("10.0.30.1/32", "10.0.30.1")]    // a /32 with no defined host -> the plain IP
    [InlineData("10.0.30.5/32", "10.0.30.5")]
    [InlineData("10.0.30.255/32", "10.0.30.255")]
    [InlineData("10.0.30.128/25", "part of China (10.0.30.128/25)")] // contained sub-range
    public void Resolve_names_matching_networks(string cidr, string expected)
        => Assert.Equal(expected, Resolver().Resolve(cidr));

    [Fact]
    public void Resolve_names_a_defined_host_by_its_name()
    {
        var resolver = new NetworkNameResolver
        {
            Networks = new[]
            {
                new NamedNetwork("China", "10.0.30.0/24")
                {
                    Hosts = new[] { new NamedHost("WebServer", "10.0.30.5") },
                },
            },
        };

        Assert.Equal("China / WebServer", resolver.Resolve("10.0.30.5/32"));
        // A different /32 in the network with no matching host falls back to the plain IP.
        Assert.Equal("10.0.30.6", resolver.Resolve("10.0.30.6/32"));
    }

    [Fact]
    public void Resolve_uses_the_ip_when_a_matching_host_has_no_name()
    {
        var resolver = new NetworkNameResolver
        {
            Networks = new[]
            {
                new NamedNetwork("China", "10.0.30.0/24")
                {
                    Hosts = new[] { new NamedHost("", "10.0.30.7") },
                },
            },
        };

        Assert.Equal("10.0.30.7", resolver.Resolve("10.0.30.7/32"));
    }

    [Theory]
    [InlineData("10.0.99.0/24")]  // no alias covers this
    [InlineData("10.0.30.0/23")]  // broader than China, not contained
    [InlineData("not-a-cidr")]    // unparseable
    [InlineData("")]
    [InlineData("-")]
    public void Resolve_returns_null_when_no_match(string cidr)
        => Assert.Null(Resolver().Resolve(cidr));

    [Fact]
    public void Resolve_prefers_exact_network_over_host_form_for_32_alias()
    {
        var resolver = new NetworkNameResolver
        {
            Networks = new[] { new NamedNetwork("Gateway", "10.0.30.1/32") },
        };
        Assert.Equal("Gateway", resolver.Resolve("10.0.30.1/32"));
    }
}
