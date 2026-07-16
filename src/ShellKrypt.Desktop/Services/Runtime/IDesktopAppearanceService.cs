using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Services.Runtime;

public interface IDesktopAppearanceService
{
    void ApplyTheme(string themeId);
    void ApplyLocalization(LocalizationService localization);
}
