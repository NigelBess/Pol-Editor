using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PolFileEditor.Models;
using PolFileEditor.Services;
using PolFileEditor.ViewModels;
using PolFileEditor.Views;
using Xunit;

namespace PolFileEditor.Tests;

/// <summary>
/// End-to-end check that the "known networks" dropdown on an IP field actually fills the
/// field when a network is clicked — exercising the real DataTemplate, flyout, and the
/// code-behind Click handler in <see cref="MainWindow"/>.
/// </summary>
public class QuickPickFlyoutUiTests
{
    [Fact]
    public async Task Clicking_a_known_network_fills_the_ip_field()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() => RunTest(pickHost: false), CancellationToken.None);
    }

    [Fact]
    public async Task Clicking_a_specific_host_fills_the_ip_field_as_a_32()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
        await session.Dispatch(() => RunTest(pickHost: true), CancellationToken.None);
    }

    private static void RunTest(bool pickHost)
    {
        var vm = new MainWindowViewModel(new NullFileDialogService());

        // Start from a clean document (a freshly reopened file must not leave stray rules).
        vm.NewCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // One named network with a host, one rule to host the IP fields.
        vm.AddNetworkCommand.Execute(null);
        vm.KnownNetworks[0].Name = "China";
        vm.KnownNetworks[0].Cidr.Value = "10.0.30.0/24";
        vm.KnownNetworks[0].AddHostCommand.Execute(null);
        vm.KnownNetworks[0].Hosts[0].Name = "WebServer";
        vm.KnownNetworks[0].Hosts[0].Ip.Value = "10.0.30.5";
        vm.AddTaskCommand.Execute(null);
        vm.Tasks[0].AddRuleCommand.Execute(null);

        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The IP fields are the only ones carrying a quick-pick dropdown.
        var dropDown = window.GetVisualDescendants()
            .OfType<DropDownButton>()
            .First(d => d.DataContext is ValidatedField { HasQuickPicks: true });
        var field = (ValidatedField)dropDown.DataContext!;
        Assert.Equal("", field.Value);

        // Open the flyout and let its content realize.
        dropDown.Flyout!.ShowAt(dropDown);
        Dispatcher.UIThread.RunJobs();

        if (((Flyout)dropDown.Flyout).Content is not Control content)
            throw new Xunit.Sdk.XunitException("flyout content not realized");

        var pick = pickHost
            ? content.GetVisualDescendants().OfType<Button>().First(b => b.DataContext is NamedHost)
            : content.GetVisualDescendants().OfType<Button>().First(b => b.DataContext is NamedNetwork);

        pick.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(pickHost ? "10.0.30.5/32" : "10.0.30.0/24", field.Value);
    }
}
