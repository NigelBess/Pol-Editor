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

    [RelayCommand]
    private void RemoveSelf() => RemoveRequested?.Invoke(this, EventArgs.Empty);

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
    public ValidatedField IpProtocol { get; }
    public ValidatedField PortSource { get; }
    public ValidatedField PortDest { get; }

    public IReadOnlyList<ValidatedField> Fields { get; }

    public RuleViewModel()
    {
        MacSource = new ValidatedField("MAC Source", Validators.Mac);
        MacDest = new ValidatedField("MAC Dest", Validators.Mac);
        IpSource = new ValidatedField("IP Source", Validators.Cidr);
        IpDest = new ValidatedField("IP Dest", Validators.Cidr);
        IpProtocol = new ValidatedField("IP Protocol", Validators.Protocol);
        PortSource = new ValidatedField("Port Source", Validators.Port, isNumeric: true);
        PortDest = new ValidatedField("Port Dest", Validators.Port, isNumeric: true);

        Fields = new[] { MacSource, MacDest, IpSource, IpDest, IpProtocol, PortSource, PortDest };
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
            var proto = IpProtocol.Value.Trim();
            if (hasPort && proto != "6" && proto != "17")
            {
                return "A port is specified but the IP protocol is not TCP (6) or UDP (17). "
                     + "Ports only apply to TCP/UDP traffic.";
            }
            return null;
        }
    }

    public bool HasRuleWarning => RuleWarning != null;

    public PolRule ToModel() => new()
    {
        Action = Action,
        MacSource = MacSource.Value,
        MacDest = MacDest.Value,
        IpSource = IpSource.Value,
        IpDest = IpDest.Value,
        IpProtocol = IpProtocol.Value,
        PortSource = PortSource.Value,
        PortDest = PortDest.Value,
        Comment = Comment
    };

    public static RuleViewModel FromModel(PolRule model)
    {
        var vm = new RuleViewModel { Action = model.Action, Comment = model.Comment };
        vm.MacSource.SetInitial(model.MacSource);
        vm.MacDest.SetInitial(model.MacDest);
        vm.IpSource.SetInitial(model.IpSource);
        vm.IpDest.SetInitial(model.IpDest);
        vm.IpProtocol.SetInitial(model.IpProtocol);
        vm.PortSource.SetInitial(model.PortSource);
        vm.PortDest.SetInitial(model.PortDest);
        return vm;
    }

    private void OnFieldChanged(object? sender, EventArgs e) => RaiseChanged();

    partial void OnActionChanged(string value) => RaiseChanged();

    partial void OnCommentChanged(string value) => RaiseChanged();

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(WorstSeverity));
        OnPropertyChanged(nameof(RuleWarning));
        OnPropertyChanged(nameof(HasRuleWarning));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
