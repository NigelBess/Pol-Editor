namespace PolFileEditor.Services;

/// <summary>User's choice when prompted about unsaved changes.</summary>
public enum SavePrompt
{
    // Cancel first so closing the dialog via the window "X" (which returns default(T))
    // is treated as Cancel rather than Save.
    Cancel,
    Save,
    Discard
}

/// <summary>
/// Abstracts the window-owned interactions (file pickers, dialogs, close) so the
/// view model stays testable and free of Avalonia UI types. Implemented by MainWindow.
/// </summary>
public interface IFileDialogService
{
    /// <summary>Shows an open-file picker; returns the chosen path or null if cancelled.</summary>
    Task<string?> ShowOpenAsync();

    /// <summary>Shows a save-file picker; returns the chosen path or null if cancelled.</summary>
    Task<string?> ShowSaveAsync(string? suggestedFileName);

    /// <summary>Prompts about unsaved changes (Save / Discard / Cancel).</summary>
    Task<SavePrompt> ConfirmUnsavedAsync();

    /// <summary>Shows an error message to the user.</summary>
    Task ShowErrorAsync(string title, string message);

    /// <summary>Requests the main window to close.</summary>
    void RequestClose();
}
