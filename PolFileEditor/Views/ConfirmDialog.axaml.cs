using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PolFileEditor.Services;

namespace PolFileEditor.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public static Task<SavePrompt> ShowUnsavedAsync(Window owner)
    {
        var dlg = new ConfirmDialog();
        dlg.TitleText.Text = "Unsaved changes";
        dlg.MessageText.Text = "This document has unsaved changes. Do you want to save them before continuing?";
        dlg.AddButton("Save", SavePrompt.Save, isDefault: true);
        dlg.AddButton("Don't Save", SavePrompt.Discard);
        dlg.AddButton("Cancel", SavePrompt.Cancel, isCancel: true);
        return dlg.ShowDialog<SavePrompt>(owner);
    }

    public static async Task ShowErrorAsync(Window owner, string title, string message)
    {
        var dlg = new ConfirmDialog { Title = title };
        dlg.TitleText.Text = title;
        dlg.MessageText.Text = message;
        dlg.AddButton("OK", SavePrompt.Cancel, isDefault: true, isCancel: true);
        await dlg.ShowDialog<SavePrompt>(owner);
    }

    private void AddButton(string text, SavePrompt result, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 96,
            IsDefault = isDefault,
            IsCancel = isCancel
        };
        button.Click += (_, _) => Close(result);
        ButtonsPanel.Children.Add(button);
    }
}
