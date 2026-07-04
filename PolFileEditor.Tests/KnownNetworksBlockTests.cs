using PolFileEditor.Models;
using Xunit;

namespace PolFileEditor.Tests;

public class KnownNetworksBlockTests
{
    [Fact]
    public void Extract_pulls_entries_and_removes_the_block_lines()
    {
        var rawHeader = string.Join("\n",
            "# My firewall header",
            "# Known Networks:",
            "#     China: 10.0.30.0/24",
            "#     DC: 10.0.40.0/24");

        var networks = new List<NamedNetwork>();
        var remaining = KnownNetworksBlock.Extract(rawHeader, networks);

        Assert.Equal("# My firewall header", remaining);
        Assert.Equal(
            new[] { new NamedNetwork("China", "10.0.30.0/24"), new NamedNetwork("DC", "10.0.40.0/24") },
            networks);
    }

    [Fact]
    public void Extract_stops_at_first_non_entry_line()
    {
        var rawHeader = string.Join("\n",
            "# Known Networks:",
            "#     China: 10.0.30.0/24",
            "# Some other note");

        var networks = new List<NamedNetwork>();
        var remaining = KnownNetworksBlock.Extract(rawHeader, networks);

        Assert.Equal(new[] { new NamedNetwork("China", "10.0.30.0/24") }, networks);
        Assert.Equal("# Some other note", remaining);
    }

    [Fact]
    public void Extract_returns_header_unchanged_when_no_block()
    {
        var networks = new List<NamedNetwork>();
        var remaining = KnownNetworksBlock.Extract("# just a header", networks);

        Assert.Empty(networks);
        Assert.Equal("# just a header", remaining);
    }

    [Fact]
    public void ToComments_renders_the_block()
    {
        var expected = string.Join("\n",
            "# Known Networks:",
            "#     China: 10.0.30.0/24",
            "#     DC: 10.0.40.0/24");

        var actual = KnownNetworksBlock.ToComments(new[]
        {
            new NamedNetwork("China", "10.0.30.0/24"),
            new NamedNetwork("DC", "10.0.40.0/24"),
        });

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToComments_is_empty_for_no_networks()
        => Assert.Equal("", KnownNetworksBlock.ToComments(Array.Empty<NamedNetwork>()));

    [Fact]
    public void Serialize_places_block_after_header_and_before_first_task()
    {
        var doc = new PolDocument { HeaderText = "Hello header" };
        doc.KnownNetworks.Add(new NamedNetwork("China", "10.0.30.0/24"));
        var task = new PolTask { Number = 1 };
        task.Rules.Add(new PolRule { Action = "Allow", IpDest = "10.0.30.0/24" });
        doc.Tasks.Add(task);

        var text = PolSerializer.Serialize(doc);

        var headerPos = text.IndexOf("# Hello header", StringComparison.Ordinal);
        var blockPos = text.IndexOf("# Known Networks:", StringComparison.Ordinal);
        var taskPos = text.IndexOf("# Task 1", StringComparison.Ordinal);

        Assert.True(headerPos >= 0 && blockPos >= 0 && taskPos >= 0);
        Assert.True(headerPos < blockPos, "header should come before the block");
        Assert.True(blockPos < taskPos, "block should come before the first task");
    }

    [Fact]
    public void Round_trip_preserves_networks_and_header()
    {
        var doc = new PolDocument { HeaderText = "Hello header" };
        doc.KnownNetworks.Add(new NamedNetwork("China", "10.0.30.0/24"));
        doc.KnownNetworks.Add(new NamedNetwork("DC", "10.0.40.0/24"));
        var task = new PolTask { Number = 1 };
        task.Rules.Add(new PolRule { Action = "Allow", IpDest = "10.0.30.0/24" });
        doc.Tasks.Add(task);

        var reparsed = PolParser.Parse(PolSerializer.Serialize(doc));

        Assert.Equal("Hello header", reparsed.HeaderText);
        Assert.Equal(
            new[] { new NamedNetwork("China", "10.0.30.0/24"), new NamedNetwork("DC", "10.0.40.0/24") },
            reparsed.KnownNetworks);
    }
}
