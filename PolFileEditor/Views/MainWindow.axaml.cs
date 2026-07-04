using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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

    /// <summary>Dismisses the "known networks" dropdown once a network has been picked (the
    /// bound command has already filled the field). The flyout is otherwise light-dismiss only.</summary>
    private void OnQuickPickClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control c &&
            c.FindLogicalAncestorOfType<DropDownButton>() is { Flyout: { } flyout })
        {
            flyout.Hide();
        }
    }
}
