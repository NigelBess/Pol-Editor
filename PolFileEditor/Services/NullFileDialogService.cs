namespace PolFileEditor.Services;

/// <summary>No-op dialog service used by the XAML design-time view model.</summary>
public sealed class NullFileDialogService : IFileDialogService
{
    public Task<string?> ShowOpenAsync() => Task.FromResult<string?>(null);
    public Task<string?> ShowSaveAsync(string? suggestedFileName) => Task.FromResult<string?>(null);
    public Task<SavePrompt> ConfirmUnsavedAsync() => Task.FromResult(SavePrompt.Cancel);
    public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    public void RequestClose() { }
}
