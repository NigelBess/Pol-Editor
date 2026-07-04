using PolFileEditor.Models;
using PolFileEditor.ViewModels;
using Xunit;

namespace PolFileEditor.Tests;

public class RuleSpecificityTests
{
    // Params: macSrc, macDst, ipSrc, ipDst, proto, portSrc, portDst, expected specificity.
    // "-" (or empty) means unused and does not count.
    [Theory]
    // Nothing specified -> matches everything -> least specific.
    [InlineData("-", "-", "-", "-", "-", "-", "-", 0)]
    // IP dest + protocol + destination port -> 3 items (the plan's worked example).
    [InlineData("-", "-", "-", "10.0.2.0/24", "6", "-", "443", 3)]
    // A single IP source.
    [InlineData("-", "-", "10.0.1.0/24", "-", "-", "-", "-", 1)]
    // Every match field specified -> maximum specificity of 7.
    [InlineData("aa:bb:cc:dd:ee:ff", "11:22:33:44:55:66", "10.0.1.0/24", "10.0.2.0/24", "6", "1024", "443", 7)]
    public void Specificity_counts_specified_match_fields(
        string macSrc, string macDst, string ipSrc, string ipDst,
        string proto, string portSrc, string portDst, int expected)
    {
        var rule = RuleViewModel.FromModel(new PolRule
        {
            Action = "Allow",
            MacSource = macSrc,
            MacDest = macDst,
            IpSource = ipSrc,
            IpDest = ipDst,
            IpProtocol = proto,
            PortSource = portSrc,
            PortDest = portDst,
        });

        Assert.Equal(expected, rule.Specificity);
    }

    [Fact]
    public void SpecificityText_formats_the_count()
    {
        var one = RuleViewModel.FromModel(new PolRule { IpDest = "10.0.2.0/24" });
        Assert.Equal("Specificity: 1", one.SpecificityText);

        var three = RuleViewModel.FromModel(new PolRule
        {
            IpDest = "10.0.2.0/24",
            IpProtocol = "6",
            PortDest = "443",
        });
        Assert.Equal("Specificity: 3", three.SpecificityText);
    }
}
