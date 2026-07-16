using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Shell.Runtime;

public sealed class DesktopAppearanceService : IDesktopAppearanceService
{
    public void ApplyTheme(string themeId)
    {
        if (Avalonia.Application.Current is App app)
            app.ApplyTheme(themeId);
    }

    public void ApplyLocalization(LocalizationService localization)
    {
        if (Avalonia.Application.Current is App app)
            app.ApplyLocalization(localization);
    }
}
