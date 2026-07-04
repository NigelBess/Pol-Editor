using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolFileEditor.Models;

namespace PolFileEditor.ViewModels;

/// <summary>A task group: an auto-numbered container of rules.</summary>
public partial class TaskViewModel : ObservableObject
{
    /// <summary>Shared resolver injected by the owner and handed down to each rule so
    /// their summaries can name known networks.</summary>
    public NetworkNameResolver? Resolver { get; set; }

    /// <summary>Raised when a rule is added/removed or any rule changes (bubbles up).</summary>
    public event EventHandler? StructureChanged;
    public event EventHandler? ContentChanged;

    /// <summary>Raised when the user clicks this task's remove button.</summary>
    public event EventHandler? RemoveRequested;

    [RelayCommand]
    private void RemoveSelf() => RemoveRequested?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private int _number;

    public ObservableCollection<RuleViewModel> Rules { get; } = new();

    public string Title => $"Task {Number}";

    [RelayCommand]
    private void AddRule() => AddRule(new RuleViewModel());

    public void AddRule(RuleViewModel rule)
    {
        Attach(rule);
        Rules.Add(rule);
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Attach(RuleViewModel rule)
    {
        rule.Resolver = Resolver;
        rule.Changed += OnRuleChanged;
        rule.RemoveRequested += OnRuleRemoveRequested;
        rule.DuplicateRequested += OnRuleDuplicateRequested;
    }

    private void Detach(RuleViewModel rule)
    {
        rule.Changed -= OnRuleChanged;
        rule.RemoveRequested -= OnRuleRemoveRequested;
        rule.DuplicateRequested -= OnRuleDuplicateRequested;
    }

    private void OnRuleRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is RuleViewModel rule) RemoveRule(rule);
    }

    private void OnRuleDuplicateRequested(object? sender, EventArgs e)
    {
        if (sender is not RuleViewModel rule) return;

        var clone = RuleViewModel.FromModel(rule.ToModel());
        Attach(clone);
        Rules.Insert(Rules.IndexOf(rule) + 1, clone);
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveRule(RuleViewModel rule)
    {
        Detach(rule);
        Rules.Remove(rule);
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnRuleChanged(object? sender, EventArgs e)
        => ContentChanged?.Invoke(this, EventArgs.Empty);
}
