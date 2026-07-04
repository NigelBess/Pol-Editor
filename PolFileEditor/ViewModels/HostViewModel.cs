using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolFileEditor.Models;

namespace PolFileEditor.ViewModels;

/// <summary>
/// One host defined inside a known network: an optional friendly name and a validated bare
/// IPv4 address (validated to fall inside the owning network's subnet). Edited under its
/// network in the "Known Networks" section, with the same add/delete/duplicate buttons as rules.
/// </summary>
public partial class HostViewModel : ObservableObject
{
    /// <summary>Raised when the name or IP changes (so summaries/counts can be refreshed).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the user clicks this host's remove button.</summary>
    public event EventHandler? RemoveRequested;

    /// <summary>Raised when the user clicks this host's duplicate button.</summary>
    public event EventHandler? DuplicateRequested;

    [RelayCommand]
    private void RemoveSelf() => RemoveRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void DuplicateSelf() => DuplicateRequested?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    private string _name = "";

    public ValidatedField Ip { get; }

    /// <param name="ipValidator">Validates the IP against the owning network's subnet. Supplied
    /// by the network so it stays bound to that network's (live) CIDR.</param>
    public HostViewModel(Func<string, ValidationResult> ipValidator)
    {
        Ip = new ValidatedField("IP", ipValidator);
        Ip.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnNameChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);

    public NamedHost ToModel() => new(Name.Trim(), Ip.Value.Trim());
}
