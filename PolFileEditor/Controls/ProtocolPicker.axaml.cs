using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PolFileEditor.Models;
using PolFileEditor.Views;

namespace PolFileEditor.Controls;

/// <summary>
/// Protocol selector: a combo box offering Unused + the three recommended protocols
/// (ICMP/TCP/UDP), plus a "More options…" entry that opens a searchable dialog with the
/// full IANA list. The chosen protocol is exposed via <see cref="SelectedProtocol"/>.
/// </summary>
public partial class ProtocolPicker : UserControl
{
    public static readonly StyledProperty<ProtocolOption?> SelectedProtocolProperty =
        AvaloniaProperty.Register<ProtocolPicker, ProtocolOption?>(
            nameof(SelectedProtocol), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private ComboBox _combo = null!;
    private bool _suppress;

    public ProtocolPicker()
    {
        InitializeComponent();
        _combo = this.FindControl<ComboBox>("Combo")!;
        _combo.SelectionChanged += OnSelectionChanged;
        RebuildItems();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public ProtocolOption? SelectedProtocol
    {
        get => GetValue(SelectedProtocolProperty);
        set => SetValue(SelectedProtocolProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedProtocolProperty)
            RebuildItems();
    }

    /// <summary>Rebuilds the short combo list so it always contains the current selection.</summary>
    private void RebuildItems()
    {
        var current = SelectedProtocol ?? ProtocolCatalog.Unused;

        var items = new List<ProtocolOption>(ProtocolCatalog.QuickOptions);
        if (!items.Contains(current) && current != ProtocolCatalog.More)
            items.Add(current);
        items.Add(ProtocolCatalog.More);

        _suppress = true;
        _combo.ItemsSource = items;
        _combo.SelectedItem = items.Contains(current) ? current : ProtocolCatalog.Unused;
        _suppress = false;
    }

    private async void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppress)
            return;

        var chosen = _combo.SelectedItem as ProtocolOption;

        if (chosen == ProtocolCatalog.More)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            var result = owner is null ? null : await ProtocolSearchDialog.ShowAsync(owner);

            if (result is not null)
                SelectedProtocol = result; // OnPropertyChanged -> RebuildItems
            else
                RebuildItems();            // revert the combo away from "More options…"
            return;
        }

        if (chosen is not null)
            SelectedProtocol = chosen;
    }
}
