using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PolFileEditor.Models;

namespace PolFileEditor.Views;

public partial class ProtocolSearchDialog : Window
{
    private readonly ObservableCollection<ProtocolOption> _results = new();

    public ProtocolSearchDialog()
    {
        InitializeComponent();

        ResultsList.ItemsSource = _results;
        ApplyFilter(string.Empty);

        SearchBox.TextChanged += (_, _) => ApplyFilter(SearchBox.Text ?? string.Empty);
        OkButton.Click += (_, _) => Accept();
        CancelButton.Click += (_, _) => Close(null);
        ResultsList.DoubleTapped += OnResultsDoubleTapped;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public static Task<ProtocolOption?> ShowAsync(Window owner)
    {
        var dialog = new ProtocolSearchDialog();
        return dialog.ShowDialog<ProtocolOption?>(owner);
    }

    private void ApplyFilter(string query)
    {
        query = query.Trim();
        _results.Clear();
        foreach (var option in ProtocolCatalog.Options)
        {
            if (query.Length == 0 ||
                option.Display.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                option.Value == query)
            {
                _results.Add(option);
            }
        }

        if (_results.Count > 0)
            ResultsList.SelectedIndex = 0;
    }

    private void OnResultsDoubleTapped(object? sender, TappedEventArgs e) => Accept();

    private void Accept()
    {
        if (ResultsList.SelectedItem is ProtocolOption option)
            Close(option);
    }
}
