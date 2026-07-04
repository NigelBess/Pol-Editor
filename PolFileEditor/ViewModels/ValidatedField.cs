using CommunityToolkit.Mvvm.ComponentModel;
using PolFileEditor.Models;

namespace PolFileEditor.ViewModels;

/// <summary>
/// A single editable rule field: its label, current value, and live validation result.
/// The view binds <see cref="Value"/> to a TextBox and colors it by <see cref="Severity"/>,
/// showing <see cref="Message"/> as the explanation. Numeric fields reject non-digit input.
/// </summary>
public sealed class ValidatedField : ObservableObject
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
}
