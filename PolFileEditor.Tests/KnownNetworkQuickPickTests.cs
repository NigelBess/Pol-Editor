using System.Collections.ObjectModel;
using PolFileEditor.Models;
using PolFileEditor.ViewModels;
using Xunit;

namespace PolFileEditor.Tests;

public class KnownNetworkQuickPickTests
{
    [Fact]
    public void Only_ip_fields_offer_quick_picks()
    {
        var rule = new RuleViewModel { KnownNetworks = new ObservableCollection<NamedNetwork>() };

        Assert.True(rule.IpSource.HasQuickPicks);
        Assert.True(rule.IpDest.HasQuickPicks);
        Assert.False(rule.MacSource.HasQuickPicks);
        Assert.False(rule.PortDest.HasQuickPicks);
    }

    [Fact]
    public void Applying_a_pick_fills_the_field_with_its_cidr()
    {
        var networks = new ObservableCollection<NamedNetwork> { new("China", "10.0.30.0/24") };
        var rule = new RuleViewModel { KnownNetworks = networks };

        rule.IpDest.ApplyQuickPickCommand.Execute(networks[0]);

        Assert.Equal("10.0.30.0/24", rule.IpDest.Value);
    }

    [Fact]
    public void Quick_picks_share_the_owner_list_so_edits_appear_live()
    {
        var networks = new ObservableCollection<NamedNetwork>();
        var rule = new RuleViewModel { KnownNetworks = networks };

        // A network added after wiring is visible through the field's shared collection.
        networks.Add(new NamedNetwork("Datacenter", "10.0.40.0/24"));

        Assert.Same(networks, rule.IpSource.QuickPicks);
        Assert.Contains(new NamedNetwork("Datacenter", "10.0.40.0/24"), rule.IpSource.QuickPicks!);
    }
}
