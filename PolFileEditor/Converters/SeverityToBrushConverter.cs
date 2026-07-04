using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PolFileEditor.Models;

namespace PolFileEditor.Converters;

/// <summary>Maps a <see cref="Severity"/> to a border/foreground brush.</summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x00));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            Severity.Error => ErrorBrush,
            Severity.Warning => WarningBrush,
            _ => Brushes.Transparent // no highlight when the field is valid
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
