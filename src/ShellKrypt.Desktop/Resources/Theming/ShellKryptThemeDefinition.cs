using Avalonia.Styling;

namespace ShellKrypt.Desktop.Resources.Theming;

public sealed record ShellKryptThemeDefinition(
    string Id,
    string DisplayName,
    ThemeVariant BaseVariant,
    IReadOnlyDictionary<string, string> Palette);
