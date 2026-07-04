using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PolFileEditor.Models;

namespace PolFileEditor.ViewModels;

/// <summary>
/// A single editable rule field: its label, current value, and live validation result.
/// The view binds <see cref="Value"/> to a TextBox and colors it by <see cref="Severity"/>,
/// showing <see cref="Message"/> as the explanation. Numeric fields reject non-digit input.
/// </summary>
public sealed partial class ValidatedField : ObservableObject
{
    private readonly Func<string, ValidationResult> _validator;
    private string _value = "";
    private ValidationResult _result = ValidationResult.Valid;

    /// <summary>Raised whenever the value (and therefore validation) changes.</summary>
    public event EventHandler? Changed;

    public ValidatedField(string label, Func<string, ValidationResult> validator, bool isNumeric = false)
    {
        Label = label;
        _validator = validator;
        IsNumeric = isNumeric;
    }

    public string Label { get; }

    public bool IsNumeric { get; }

    public string Value
    {
        get => _value;
        set
        {
            var raw = value ?? "";
            var filtered = IsNumeric ? new string(raw.Where(char.IsDigit).ToArray()) : raw;
            var valueChanged = !string.Equals(filtered, _value, StringComparison.Ordinal);

            _value = filtered;
            _result = _validator(_value);

            // Always re-assert Value so a rejected keystroke snaps the TextBox back to the
            // filtered text even when filtering produced the previous value.
            OnPropertyChanged(nameof(Value));

            if (valueChanged)
            {
                OnPropertyChanged(nameof(Severity));
                OnPropertyChanged(nameof(Message));
                OnPropertyChanged(nameof(HasMessage));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public Severity Severity => _result.Severity;

    public string Message => _result.Message;

    public bool HasMessage => !string.IsNullOrEmpty(_result.Message);

    /// <summary>Sets the value without any change notification bookkeeping side effects
    /// beyond the normal ones (used when loading a document).</summary>
    public void SetInitial(string value) => Value = value ?? "";

    /// <summary>Re-runs the validator against the current (unchanged) value and refreshes the
    /// displayed result. Used when validation depends on external state — e.g. a host IP whose
    /// validity depends on its network's CIDR being edited elsewhere.</summary>
    public void Revalidate()
    {
        _result = _validator(_value);
        OnPropertyChanged(nameof(Severity));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(HasMessage));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ---- Known-network quick fill -------------------------------------------
    // Set (once, by the owner) on IP fields only; the view shows a "known networks"
    // dropdown that fills this field's value with a chosen network's CIDR. Left null
    // on the MAC/port fields, where it makes no sense.

    private ObservableCollection<NamedNetwork>? _quickPicks;

    /// <summary>The named networks offered as one-click fills, or null if this field has none.
    /// A shared, live collection: additions/edits in the "Known networks" section appear here.</summary>
    public ObservableCollection<NamedNetwork>? QuickPicks
    {
        get => _quickPicks;
        set
        {
            _quickPicks = value;
            OnPropertyChanged(nameof(QuickPicks));
            OnPropertyChanged(nameof(HasQuickPicks));
        }
    }

    /// <summary>True when this field offers known-network quick fills (IP fields only).</summary>
    public bool HasQuickPicks => _quickPicks is not null;

    /// <summary>Fills this field with a known network's CIDR.</summary>
    [RelayCommand]
    private void ApplyQuickPick(NamedNetwork? net)
    {
        if (net is not null)
            Value = net.Cidr;
    }

    /// <summary>Fills this field with a specific host's address as a /32 CIDR.</summary>
    [RelayCommand]
    private void ApplyHostPick(NamedHost? host)
    {
        if (host is not null)
            Value = host.Ip + "/32";
    }
}
