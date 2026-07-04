using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolFileEditor.Models;
using PolFileEditor.Services;

namespace PolFileEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _currentPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isDirty;

    [ObservableProperty]
    private string _headerText = "";

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    public ObservableCollection<TaskViewModel> Tasks { get; } = new();

    public string WindowTitle
    {
        get
        {
            var name = CurrentPath is null ? "Untitled" : Path.GetFileName(CurrentPath);
            return $"{(IsDirty ? "*" : "")}{name} - Pol File Editor";
        }
    }

    // Parameterless ctor for the XAML designer.
    public MainWindowViewModel() : this(new NullFileDialogService()) { }

    public MainWindowViewModel(IFileDialogService dialogs)
    {
        _dialogs = dialogs;
        NewDocument(seedHeader: true);
        IsDirty = false;
    }

    // ---- Structure commands -------------------------------------------------

    [RelayCommand]
    private void AddTask()
    {
        AttachTask(new TaskViewModel());
        Renumber();
        MarkDirty();
    }

    private void RemoveTask(TaskViewModel task)
    {
        DetachTask(task);
        Tasks.Remove(task);
        Renumber();
        MarkDirty();
    }

    // ---- File commands ------------------------------------------------------

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        NewDocument(seedHeader: true);
        CurrentPath = null;
        IsDirty = false;
        RecomputeCounts();
        StatusMessage = "New document.";
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardAsync()) return;

        var path = await _dialogs.ShowOpenAsync();
        if (path is null) return;

        try
        {
            var text = await File.ReadAllTextAsync(path);
            LoadDocument(PolParser.Parse(text));
            CurrentPath = path;
            IsDirty = false;
            RecomputeCounts();
            StatusMessage = $"Opened {Path.GetFileName(path)} ({Tasks.Count} task(s)).";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Open failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveAsync() => await SaveToPathAsync(CurrentPath);

    [RelayCommand]
    private async Task SaveAsAsync() => await SaveToPathAsync(null);

    [RelayCommand]
    private async Task ExitAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        _dialogs.RequestClose();
    }

    /// <summary>Called by the window's Closing handler. Returns true if it's safe to close.</summary>
    public async Task<bool> CanCloseAsync() => await ConfirmDiscardAsync();

    private async Task SaveToPathAsync(string? path)
    {
        if (path is null)
        {
            path = await _dialogs.ShowSaveAsync(CurrentPath is null ? "configure.pol" : Path.GetFileName(CurrentPath));
            if (path is null) return;
        }

        try
        {
            await File.WriteAllTextAsync(path, PolSerializer.Serialize(BuildModel()));
            CurrentPath = path;
            IsDirty = false;
            StatusMessage = $"Saved {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Save failed", ex.Message);
        }
    }

    /// <summary>Returns true to proceed, false to cancel the pending action.</summary>
    private async Task<bool> ConfirmDiscardAsync()
    {
        if (!IsDirty) return true;

        switch (await _dialogs.ConfirmUnsavedAsync())
        {
            case SavePrompt.Save:
                await SaveToPathAsync(CurrentPath);
                return !IsDirty; // if the save was cancelled, don't proceed
            case SavePrompt.Discard:
                return true;
            default:
                return false;
        }
    }

    // ---- Document (de)serialization ----------------------------------------

    private void NewDocument(bool seedHeader)
    {
        foreach (var task in Tasks.ToList())
            DetachTask(task);
        Tasks.Clear();
        HeaderText = seedHeader ? PolDocument.DefaultHeader.TrimEnd('\n') : "";
        RecomputeCounts();
    }

    private void LoadDocument(PolDocument doc)
    {
        NewDocument(seedHeader: false);
        HeaderText = doc.HeaderText;

        foreach (var polTask in doc.Tasks)
        {
            var taskVm = new TaskViewModel();
            AttachTask(taskVm);
            foreach (var polRule in polTask.Rules)
                taskVm.AddRule(RuleViewModel.FromModel(polRule));
        }

        Renumber();
    }

    private PolDocument BuildModel()
    {
        var doc = new PolDocument { HeaderText = HeaderText };
        foreach (var taskVm in Tasks)
        {
            var polTask = new PolTask { Number = taskVm.Number };
            foreach (var ruleVm in taskVm.Rules)
                polTask.Rules.Add(ruleVm.ToModel());
            doc.Tasks.Add(polTask);
        }
        return doc;
    }

    // ---- Bookkeeping --------------------------------------------------------

    private void AttachTask(TaskViewModel task)
    {
        task.StructureChanged += OnTaskStructureChanged;
        task.ContentChanged += OnTaskContentChanged;
        task.RemoveRequested += OnTaskRemoveRequested;
        Tasks.Add(task);
    }

    private void DetachTask(TaskViewModel task)
    {
        task.StructureChanged -= OnTaskStructureChanged;
        task.ContentChanged -= OnTaskContentChanged;
        task.RemoveRequested -= OnTaskRemoveRequested;
    }

    private void OnTaskRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is TaskViewModel task) RemoveTask(task);
    }

    private void OnTaskStructureChanged(object? sender, EventArgs e)
    {
        Renumber();
        RecomputeCounts();
        MarkDirty();
    }

    private void OnTaskContentChanged(object? sender, EventArgs e)
    {
        RecomputeCounts();
        MarkDirty();
    }

    /// <summary>Assigns canonical numbers: Task index -&gt; N, rule index -&gt; N.M.</summary>
    private void Renumber()
    {
        for (var t = 0; t < Tasks.Count; t++)
        {
            var task = Tasks[t];
            task.Number = t + 1;
            for (var r = 0; r < task.Rules.Count; r++)
                task.Rules[r].RuleNumber = $"{t + 1}.{r}";
        }
    }

    private void RecomputeCounts()
    {
        var errors = 0;
        var warnings = 0;
        foreach (var task in Tasks)
        foreach (var rule in task.Rules)
        {
            foreach (var field in rule.Fields)
            {
                if (field.Severity == Severity.Error) errors++;
                else if (field.Severity == Severity.Warning) warnings++;
            }
            if (rule.HasRuleWarning) warnings++;
        }

        ErrorCount = errors;
        WarningCount = warnings;
    }

    private void MarkDirty() => IsDirty = true;

    partial void OnHeaderTextChanged(string value) => MarkDirty();
}
