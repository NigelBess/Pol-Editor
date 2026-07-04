using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PolFileEditor.Models;
using PolFileEditor.Services;
using PolFileEditor.ViewModels;

namespace PolFileEditor.Views;

public partial class MainWindow : Window, IFileDialogService
{
    private static readonly FilePickerFileType PolFileType =
        new("Pol files") { Patterns = new[] { "*.pol" } };

    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(this);
        Closing += OnClosing;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || DataContext is not MainWindowViewModel vm)
            return;

        // Cancel the close, ask the view model, and re-issue the close if allowed.
        e.Cancel = true;
        if (await vm.CanCloseAsync())
        {
            _forceClose = true;
            Close();
        }
    }

    public async Task<string?> ShowOpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open .pol file",
            AllowMultiple = false,
            FileTypeFilter = new[] { PolFileType, FilePickerFileTypes.All }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> ShowSaveAsync(string? suggestedFileName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save .pol file",
            SuggestedFileName = suggestedFileName ?? "configure.pol",
            DefaultExtension = "pol",
            FileTypeChoices = new[] { PolFileType }
        });

        return file?.TryGetLocalPath();
    }

    public Task<SavePrompt> ConfirmUnsavedAsync() => ConfirmDialog.ShowUnsavedAsync(this);

    public Task ShowErrorAsync(string title, string message)
        => ConfirmDialog.ShowErrorAsync(this, title, message);

    public void RequestClose()
    {
        _forceClose = true;
        Close();
    }

    /// <summary>Fills the field with the picked known network (or one of its hosts) and dismisses
    /// the dropdown. Done here (not via a bound command) because a Flyout's popup does not reliably
    /// inherit the target's DataContext. The owning field is the DropDownButton's own DataContext —
    /// the DropDownButton lives in the ValidatedField template (in the main tree, not the popup),
    /// so that is reliable; the clicked value comes from the button's bound DataContext.</summary>
    private void OnQuickPickClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control row)
            return;

        var dropDown = row.FindLogicalAncestorOfType<DropDownButton>();
        if (dropDown?.DataContext is ValidatedField field)
        {
            switch (row.DataContext)
            {
                case NamedNetwork net:
                    field.ApplyQuickPickCommand.Execute(net);
                    break;
                case NamedHost host:
                    field.ApplyHostPickCommand.Execute(host);
                    break;
            }
        }

        // Best-effort dismiss; the flyout is light-dismiss even if this misses.
        dropDown?.Flyout?.Hide();
    }
}
