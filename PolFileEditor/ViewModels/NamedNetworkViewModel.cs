using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolFileEditor.Models;

namespace PolFileEditor.ViewModels;

/// <summary>
/// One named-network alias row: an editable friendly name and a validated CIDR field.
/// Edited in the "Known Networks" section; persisted to the file's comment header.
/// </summary>
public partial class NamedNetworkViewModel : ObservableObject
{
    /// <summary>Raised when the name or CIDR changes (so summaries can be refreshed).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the user clicks this row's remove button.</summary>
    public event EventHandler? RemoveRequested;

    [RelayCommand]
    private void RemoveSelf() => RemoveRequested?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    private string _name = "";

    public ValidatedField Cidr { get; }

    public NamedNetworkViewModel()
    {
        Cidr = new ValidatedField("Network (CIDR)", Validators.Cidr);
        Cidr.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnNameChanged(string value) => Changed?.Invoke(this, EventArgs.Empty);

    public NamedNetwork ToModel() => new(Name.Trim(), Cidr.Value.Trim());

    public static NamedNetworkViewModel FromModel(NamedNetwork model)
    {
        var vm = new NamedNetworkViewModel { Name = model.Name };
        vm.Cidr.SetInitial(model.Cidr);
        return vm;
    }
}
