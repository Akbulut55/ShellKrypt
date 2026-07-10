using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ShellKrypt.UI.Shared.Converters;

public sealed class ThemeResourceGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrWhiteSpace(key))
            key = parameter as string;

        return !string.IsNullOrWhiteSpace(key) &&
               Application.Current?.TryGetResource(key, null, out var resource) == true &&
               resource is Geometry geometry
            ? geometry
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
