using Avalonia.Styling;

namespace ShellKrypt.UI.Shared.Theming;

public sealed record ShellKryptThemeDefinition(
    string Id,
    string DisplayName,
    ThemeVariant BaseVariant,
    IReadOnlyDictionary<string, string> Palette);
