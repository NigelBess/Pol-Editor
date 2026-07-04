using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PolFileEditor.Converters;

/// <summary>
/// Maps a task number to a stable color so each task group is visually distinct.
/// Pass ConverterParameter="fill" for a translucent background tint; otherwise returns
/// the solid accent brush (used for the border and title).
/// </summary>
public sealed class TaskColorConverter : IValueConverter
{
    // A palette of distinct hues that read acceptably in both light and dark themes.
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xE5, 0x48, 0x4D), // red
        Color.FromRgb(0xF7, 0x6B, 0x15), // orange
        Color.FromRgb(0xF5, 0xB2, 0x08), // amber
        Color.FromRgb(0x46, 0xA7, 0x58), // green
        Color.FromRgb(0x12, 0xA5, 0x94), // teal
        Color.FromRgb(0x00, 0x91, 0xFF), // blue
        Color.FromRgb(0x8E, 0x4E, 0xC6), // purple
        Color.FromRgb(0xE9, 0x3D, 0x82), // pink
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int number)
            return Brushes.Gray;

        var index = ((number - 1) % Palette.Length + Palette.Length) % Palette.Length;
        var color = Palette[index];

        if (parameter as string == "fill")
            return new SolidColorBrush(Color.FromArgb(0x26, color.R, color.G, color.B));

        return new SolidColorBrush(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
