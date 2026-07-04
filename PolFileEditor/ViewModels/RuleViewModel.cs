using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolFileEditor.Models;

namespace PolFileEditor.ViewModels;

/// <summary>
/// One firewall rule. Displays "N.M: comment" at the top (rule number read-only,
/// comment editable), an Allow/Block combo box, and the labeled match fields.
/// </summary>
public partial class RuleViewModel : ObservableObject
{
    /// <summary>Raised on any user-visible change (field edit, action, comment).</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the user clicks this rule's remove button.</summary>
    public event EventHandler? RemoveRequested;

    /// <summary>Raised when the user clicks this rule's duplicate button.</summary>
    public event EventHandler? DuplicateRequested;

    [RelayCommand]
    private void RemoveSelf() => RemoveRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void DuplicateSelf() => DuplicateRequested?.Invoke(this, EventArgs.Empty);

    [ObservableProperty]
    private string _ruleNumber = "";

    [ObservableProperty]
    private string _action = "Block";

    [ObservableProperty]
    private string _comment = "";

    public IReadOnlyList<string> ActionOptions { get; } = new[] { "Allow", "Block" };

    public ValidatedField MacSource { get; }
    public ValidatedField MacDest { get; }
    public ValidatedField IpSource { get; }
    public ValidatedField IpDest { get; }
    public ValidatedField PortSource { get; }
    public ValidatedField PortDest { get; }

    /// <summary>Selected IP protocol (chosen from the protocol picker, never invalid).</summary>
    [ObservableProperty]
    private ProtocolOption _selectedProtocol = ProtocolCatalog.Unused;

    public IReadOnlyList<ValidatedField> Fields { get; }

    /// <summary>Resolves IPs to friendly names for <see cref="Summary"/>. Set by the owner;
    /// shared across all rules so edits to the network list reflect everywhere.</summary>
    public NetworkNameResolver? Resolver { get; set; }

    public RuleViewModel()
    {
        MacSource = new ValidatedField("MAC Source", Validators.Mac);
        MacDest = new ValidatedField("MAC Dest", Validators.Mac);
        IpSource = new ValidatedField("IP Source", Validators.Cidr);
        IpDest = new ValidatedField("IP Dest", Validators.Cidr);
        PortSource = new ValidatedField("Port Source", Validators.Port, isNumeric: true);
        PortDest = new ValidatedField("Port Dest", Validators.Port, isNumeric: true);

        Fields = new[] { MacSource, MacDest, IpSource, IpDest, PortSource, PortDest };
        foreach (var field in Fields)
        {
            field.Changed += OnFieldChanged;
        }
    }

    /// <summary>Highest severity across all fields plus the rule-level cross-check.</summary>
    public Severity WorstSeverity
    {
        get
        {
            var worst = Fields.Select(f => f.Severity).DefaultIfEmpty(Severity.Ok).Max();
            if (HasRuleWarning && worst < Severity.Warning)
                worst = Severity.Warning;
            return worst;
        }
    }

    /// <summary>Cross-field check: a port is set but the protocol isn't TCP/UDP.</summary>
    public string? RuleWarning
    {
        get
        {
            var hasPort = !Validators.IsUnused(PortSource.Value) || !Validators.IsUnused(PortDest.Value);
            var proto = SelectedProtocol?.Value ?? "";
            if (hasPort && proto != "6" && proto != "17")
            {
                return "A port is specified but the IP protocol is not TCP (6) or UDP (17). "
                     + "Ports only apply to TCP/UDP traffic.";
            }
            return null;
        }
    }

    public bool HasRuleWarning => RuleWarning != null;

    /// <summary>Number of specified (non-unused) match criteria. A higher count means a
    /// more specific rule and thus a higher implicit priority.</summary>
    public int Specificity =>
        Fields.Count(f => !Validators.IsUnused(f.Value))
        + (Validators.IsUnused(SelectedProtocol?.Value) ? 0 : 1);

    /// <summary>Badge text, e.g. "Specificity: 3".</summary>
    public string SpecificityText => $"Specificity: {Specificity}";

    /// <summary>A plain-English description of this rule, shown in italics under the fields.</summary>
    public string Summary => RuleDescriber.Describe(ToModel(), Resolver);

    /// <summary>Re-raises <see cref="Summary"/> when something outside the rule changed
    /// (i.e. the known-networks list).</summary>
    public void RefreshSummary() => OnPropertyChanged(nameof(Summary));

    public PolRule ToModel() => new()
    {
        Action = Action,
        MacSource = MacSource.Value,
        MacDest = MacDest.Value,
        IpSource = IpSource.Value,
        IpDest = IpDest.Value,
        IpProtocol = SelectedProtocol?.Value ?? "",
        PortSource = PortSource.Value,
        PortDest = PortDest.Value,
        Comment = Comment
    };

    public static RuleViewModel FromModel(PolRule model)
    {
        var vm = new RuleViewModel
        {
            Action = model.Action,
            Comment = model.Comment,
            SelectedProtocol = ProtocolCatalog.FromValue(model.IpProtocol)
        };
        vm.MacSource.SetInitial(model.MacSource);
        vm.MacDest.SetInitial(model.MacDest);
        vm.IpSource.SetInitial(model.IpSource);
        vm.IpDest.SetInitial(model.IpDest);
        vm.PortSource.SetInitial(model.PortSource);
        vm.PortDest.SetInitial(model.PortDest);
        return vm;
    }

    private void OnFieldChanged(object? sender, EventArgs e) => RaiseChanged();

    partial void OnActionChanged(string value) => RaiseChanged();

    partial void OnCommentChanged(string value) => RaiseChanged();

    partial void OnSelectedProtocolChanged(ProtocolOption value) => RaiseChanged();

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(WorstSeverity));
        OnPropertyChanged(nameof(RuleWarning));
        OnPropertyChanged(nameof(HasRuleWarning));
        OnPropertyChanged(nameof(Specificity));
        OnPropertyChanged(nameof(SpecificityText));
        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
