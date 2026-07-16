using ShellKrypt.Application.Localization;

namespace ShellKrypt.Desktop.Shell.Runtime;

public interface IDesktopAppearanceService
{
    void ApplyTheme(string themeId);
    void ApplyLocalization(LocalizationService localization);
}
