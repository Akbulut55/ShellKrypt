using ShellKrypt.Application.Ports;

namespace ShellKrypt.Application.Settings;

public sealed class AppSettingsService
{
    private readonly IAppSettingsStore _store;

    public AppSettingsService(IAppSettingsStore store)
    {
        _store = store;
    }

    public AppSettings Load()
    {
        var settings = _store.Load();
        settings.NormalizeThemeId();
        settings.NormalizeLanguageId();
        settings.ApplySessionSecuritySettings(settings.ToSessionSecuritySettings());
        return settings;
    }

    public void Save(AppSettings settings)
    {
        settings.NormalizeThemeId();
        settings.NormalizeLanguageId();
        settings.ApplySessionSecuritySettings(settings.ToSessionSecuritySettings());
        _store.Save(settings);
    }
}
