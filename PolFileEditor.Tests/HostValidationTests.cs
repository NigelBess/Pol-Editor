using PolFileEditor.Models;
using Xunit;

namespace PolFileEditor.Tests;

public class HostValidationTests
{
    [Theory]
    [InlineData("10.0.30.5", "10.0.30.0/24")]   // inside the subnet
    [InlineData("10.0.30.255", "10.0.30.0/24")]
    [InlineData("10.0.40.1", "10.0.40.0/24")]
    public void In_subnet_host_is_valid(string ip, string cidr)
        => Assert.Equal(Severity.Ok, Validators.HostInNetwork(ip, cidr).Severity);

    [Theory]
    [InlineData("10.0.99.9", "10.0.30.0/24")]   // wrong subnet
    [InlineData("10.0.31.1", "10.0.30.0/24")]
    public void Out_of_subnet_host_is_an_error(string ip, string cidr)
        => Assert.Equal(Severity.Error, Validators.HostInNetwork(ip, cidr).Severity);

    [Theory]
    [InlineData("10.0.30.5/32")]   // must be a bare IP, no subnet
    [InlineData("10.0.30")]
    [InlineData("10.0.30.300")]    // octet out of range
    [InlineData("not-an-ip")]
    public void Malformed_host_ip_is_an_error(string ip)
        => Assert.Equal(Severity.Error, Validators.HostInNetwork(ip, "10.0.30.0/24").Severity);

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    public void Empty_host_is_valid(string ip)
        => Assert.Equal(Severity.Ok, Validators.HostInNetwork(ip, "10.0.30.0/24").Severity);

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cidr")]
    public void Missing_or_invalid_network_skips_the_containment_check(string cidr)
        => Assert.Equal(Severity.Ok, Validators.HostInNetwork("10.0.30.5", cidr).Severity);
}
