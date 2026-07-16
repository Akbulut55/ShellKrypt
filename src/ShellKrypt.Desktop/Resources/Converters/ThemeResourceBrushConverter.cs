using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ShellKrypt.Desktop.Resources.Converters;

public sealed class ThemeResourceBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrWhiteSpace(key))
            key = parameter as string;

        if (!string.IsNullOrWhiteSpace(key) &&
            Avalonia.Application.Current?.TryGetResource(key, null, out var resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        return Avalonia.Application.Current?.TryGetResource("TextMutedBrush", null, out var fallback) == true
            ? fallback
            : Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
