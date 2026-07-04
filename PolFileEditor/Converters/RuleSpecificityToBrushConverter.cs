using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PolFileEditor.Converters;

/// <summary>
/// Tints a rule background by its action and specificity: green for Allow, red for Block
/// (so "allow trumps block" reads at a glance), with the fill growing more opaque as the
/// rule constrains more match fields. Bound via a MultiBinding of [Action, Specificity].
/// </summary>
public sealed class RuleSpecificityToBrushConverter : IMultiValueConverter
{
    private static readonly Color Allow = Color.FromRgb(0x46, 0xA7, 0x58); // green
    private static readonly Color Block = Color.FromRgb(0xE5, 0x48, 0x4D); // red

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var action = values.ElementAtOrDefault(0) as string;
        var specificity = values.ElementAtOrDefault(1) is int s ? s : 0;

        var color = string.Equals(action?.Trim(), "Allow", StringComparison.OrdinalIgnoreCase)
            ? Allow
            : Block;

        // Ramp alpha from a faint base up to a strong cap as specificity grows (0..7).
        var alpha = (byte)Math.Min(0x14 + specificity * 0x1C, 0xC8);
        return new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
    }
}
