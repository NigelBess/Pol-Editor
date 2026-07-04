using PolFileEditor.Models;
using PolFileEditor.ViewModels;
using Xunit;

namespace PolFileEditor.Tests;

public class NamedNetworkHostsTests
{
    private static NamedNetworkViewModel Network(string cidr = "10.0.30.0/24")
    {
        var vm = new NamedNetworkViewModel { Name = "China" };
        vm.Cidr.Value = cidr;
        return vm;
    }

    [Fact]
    public void Add_host_appends_a_row()
    {
        var net = Network();
        net.AddHostCommand.Execute(null);

        Assert.Single(net.Hosts);
    }

    [Fact]
    public void Remove_host_drops_the_row()
    {
        var net = Network();
        net.AddHostCommand.Execute(null);
        var host = net.Hosts[0];

        host.RemoveSelfCommand.Execute(null);

        Assert.Empty(net.Hosts);
    }

    [Fact]
    public void Duplicate_host_inserts_a_copy_after_the_original()
    {
        var net = Network();
        net.AddHostCommand.Execute(null);
        var original = net.Hosts[0];
        original.Name = "WebServer";
        original.Ip.Value = "10.0.30.5";

        original.DuplicateSelfCommand.Execute(null);

        Assert.Equal(2, net.Hosts.Count);
        Assert.Same(original, net.Hosts[0]);
        Assert.Equal("WebServer", net.Hosts[1].Name);
        Assert.Equal("10.0.30.5", net.Hosts[1].Ip.Value);
    }

    [Fact]
    public void Host_ip_validates_against_the_network_subnet()
    {
        var net = Network();
        net.AddHostCommand.Execute(null);
        var host = net.Hosts[0];

        host.Ip.Value = "10.0.30.5";
        Assert.Equal(Severity.Ok, host.Ip.Severity);

        host.Ip.Value = "10.0.99.9";
        Assert.Equal(Severity.Error, host.Ip.Severity);
    }

    [Fact]
    public void Editing_the_network_cidr_revalidates_existing_hosts()
    {
        var net = Network("10.0.30.0/24");
        net.AddHostCommand.Execute(null);
        var host = net.Hosts[0];
        host.Ip.Value = "10.0.30.5";
        Assert.Equal(Severity.Ok, host.Ip.Severity);

        // Move the network elsewhere: the host now falls outside it.
        net.Cidr.Value = "10.0.40.0/24";

        Assert.Equal(Severity.Error, host.Ip.Severity);
    }

    [Fact]
    public void To_model_carries_named_hosts_and_drops_empty_rows()
    {
        var net = Network();
        net.AddHostCommand.Execute(null);
        net.Hosts[0].Name = "WebServer";
        net.Hosts[0].Ip.Value = "10.0.30.5";
        net.AddHostCommand.Execute(null); // left blank -> dropped

        var model = net.ToModel();

        Assert.Equal(new[] { new NamedHost("WebServer", "10.0.30.5") }, model.Hosts);
    }

    [Fact]
    public void From_model_round_trips_hosts()
    {
        var model = new NamedNetwork("China", "10.0.30.0/24")
        {
            Hosts = new[] { new NamedHost("WebServer", "10.0.30.5") },
        };

        var vm = NamedNetworkViewModel.FromModel(model);

        Assert.Equal(model, vm.ToModel());
        Assert.Equal(Severity.Ok, vm.Hosts[0].Ip.Severity);
    }
}
