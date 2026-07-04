using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolFileEditor.Models;

namespace PolFileEditor.ViewModels;

/// <summary>
/// One named-network alias: an editable friendly name, a validated CIDR, and a group of hosts
/// defined inside it. Edited in the "Known Networks" section; persisted to the file's comment
/// header.
/// </summary>
public partial class NamedNetworkViewModel : ObservableObject
{
    /// <summary>Raised when the name, CIDR, or any host changes (so summaries can be refreshed).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the user clicks this row's remove button.</summary>
    public event EventHandler? RemoveRequested;

    [RelayCommand]
    private void RemoveSelf() => RemoveRequested?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    private string _name = "";

    public ValidatedField Cidr { get; }

    public ObservableCollection<HostViewModel> Hosts { get; } = new();

    public NamedNetworkViewModel()
    {
        Cidr = new ValidatedField("Network (CIDR)", Validators.Cidr);
        Cidr.Changed += (_, _) =>
        {
            // Host validity depends on this network's subnet, so re-check every host.
            foreach (var host in Hosts)
                host.Ip.Revalidate();
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    partial void OnNameChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);

    // ---- Hosts --------------------------------------------------------------

    [RelayCommand]
    private void AddHost() => AddHost(NewHost());

    private void AddHost(HostViewModel host)
    {
        Attach(host);
        Hosts.Add(host);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates a host whose IP validates against this network's live CIDR.</summary>
    private HostViewModel NewHost()
        => new(ip => Validators.HostInNetwork(ip, Cidr.Value));

    private void Attach(HostViewModel host)
    {
        host.Changed += OnHostChanged;
        host.RemoveRequested += OnHostRemoveRequested;
        host.DuplicateRequested += OnHostDuplicateRequested;
    }

    private void Detach(HostViewModel host)
    {
        host.Changed -= OnHostChanged;
        host.RemoveRequested -= OnHostRemoveRequested;
        host.DuplicateRequested -= OnHostDuplicateRequested;
    }

    private void OnHostChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private void OnHostRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not HostViewModel host) return;
        Detach(host);
        Hosts.Remove(host);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnHostDuplicateRequested(object? sender, EventArgs e)
    {
        if (sender is not HostViewModel host) return;

        var clone = NewHost();
        clone.Name = host.Name;
        clone.Ip.SetInitial(host.Ip.Value);
        Attach(clone);
        Hosts.Insert(Hosts.IndexOf(host) + 1, clone);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ---- Model round-trip ---------------------------------------------------

    public NamedNetwork ToModel() => new(Name.Trim(), Cidr.Value.Trim())
    {
        Hosts = Hosts
            .Select(h => h.ToModel())
            .Where(h => h.Ip.Length > 0)
            .ToList()
    };

    public static NamedNetworkViewModel FromModel(NamedNetwork model)
    {
        var vm = new NamedNetworkViewModel { Name = model.Name };
        // Set the CIDR before creating hosts so their validators see a populated subnet.
        vm.Cidr.SetInitial(model.Cidr);
        foreach (var host in model.Hosts)
        {
            var hostVm = vm.NewHost();
            hostVm.Name = host.Name;
            hostVm.Ip.SetInitial(host.Ip);
            vm.Attach(hostVm);
            vm.Hosts.Add(hostVm);
        }
        return vm;
    }
}
