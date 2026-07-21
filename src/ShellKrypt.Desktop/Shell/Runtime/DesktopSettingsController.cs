using ShellKrypt.Application.Backups;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Settings;

namespace ShellKrypt.Desktop.Shell.Runtime;

public sealed class DesktopSettingsController : IDesktopSettingsController
{
    private readonly AppSettingsService _store;
    private readonly SessionSecurityService _sessionSecurity;
    private readonly IDesktopAppearanceService _appearance;
    private readonly AppSettings _settings;

    public DesktopSettingsController(
        AppSettingsService store,
        LocalizationService localization,
        SessionSecurityService sessionSecurity,
        IDesktopAppearanceService appearance)
    {
        _store = store;
        Localization = localization;
        _sessionSecurity = sessionSecurity;
        _appearance = appearance;
        _settings = store.Load();
        _settings.NormalizeThemeId();
        _settings.NormalizeLanguageId();
        _settings.NormalizeBackupCenterHistory();
        _settings.NormalizeEmergencyKit();
        _settings.NormalizeBackupSchedule();
        _settings.NormalizeMarkdownSettings();
        _sessionSecurity.ApplySettings(_settings.ToSessionSecuritySettings());
        Localization.SetLanguage(_settings.LanguageId);
        _appearance.ApplyTheme(_settings.ThemeId);
        _appearance.ApplyLocalization(Localization);
    }

    public event EventHandler? Changed;
    public LocalizationService Localization { get; }

    public bool AutoLockEnabled { get => _settings.AutoLockEnabled; set => Set(value, _settings.AutoLockEnabled, next => _settings.AutoLockEnabled = next); }
    public int AutoLockMinutes { get => _settings.AutoLockMinutes; set => Set(value, _settings.AutoLockMinutes, next => _settings.AutoLockMinutes = next); }
    public bool LockOnDeactivate { get => _settings.LockOnDeactivate; set => Set(value, _settings.LockOnDeactivate, next => _settings.LockOnDeactivate = next); }
    public int LockOnDeactivateSeconds { get => _settings.LockOnDeactivateSeconds; set => Set(value, _settings.LockOnDeactivateSeconds, next => _settings.LockOnDeactivateSeconds = next); }
    public int ClipboardClearSeconds { get => _settings.ClipboardClearSeconds; set => Set(value, _settings.ClipboardClearSeconds, next => _settings.ClipboardClearSeconds = next); }
    public bool ClipboardCopyEnabled { get => _settings.ClipboardCopyEnabled; set => Set(value, _settings.ClipboardCopyEnabled, next => _settings.ClipboardCopyEnabled = next); }
    public bool CloseToTrayEnabled { get => _settings.CloseToTrayEnabled; set => Set(value, _settings.CloseToTrayEnabled, next => _settings.CloseToTrayEnabled = next); }
    public int MarkdownAutoSaveSeconds
    {
        get => _settings.MarkdownAutoSaveSeconds;
        set => Set(AppSettings.NormalizeMarkdownAutoSaveSeconds(value), _settings.MarkdownAutoSaveSeconds, next => _settings.MarkdownAutoSaveSeconds = next);
    }

    public string ThemeId
    {
        get => _settings.ThemeId;
        set
        {
            var normalized = AppSettings.NormalizeThemeId(value);
            if (string.Equals(_settings.ThemeId, normalized, StringComparison.Ordinal))
                return;
            _settings.ThemeId = normalized;
            _appearance.ApplyTheme(normalized);
            SaveAndNotify();
        }
    }

    public string LanguageId
    {
        get => _settings.LanguageId;
        set
        {
            var normalized = AppSettings.NormalizeLanguageId(value);
            if (string.Equals(_settings.LanguageId, normalized, StringComparison.Ordinal))
                return;
            _settings.LanguageId = normalized;
            Localization.SetLanguage(normalized);
            _appearance.ApplyLocalization(Localization);
            SaveAndNotify();
        }
    }

    public bool HasAcceptedSecurityAcknowledgement => _settings.HasCurrentSecurityAcknowledgement;
    public BackupCenterHistory BackupCenterHistory => _settings.BackupCenterHistory;
    public EmergencyKitState EmergencyKit => _settings.EmergencyKit;
    public BackupScheduleSettings BackupSchedule => _settings.BackupSchedule;
    public AutomaticBackupState AutomaticBackupState => _settings.AutomaticBackupState;

    public void AcceptSecurityAcknowledgement()
    {
        if (HasAcceptedSecurityAcknowledgement)
            return;
        _settings.AcceptCurrentSecurityAcknowledgement(DateTimeOffset.UtcNow.ToString("O"));
        SaveAndNotify();
    }

    public void SaveBackupCenterHistory()
    {
        _settings.NormalizeBackupCenterHistory();
        SaveAndNotify();
    }

    public void SaveEmergencyKitState()
    {
        _settings.NormalizeEmergencyKit();
        SaveAndNotify();
    }

    public void SaveBackupScheduleState()
    {
        _settings.NormalizeBackupSchedule();
        SaveAndNotify();
    }

    private void Set<T>(T value, T current, Action<T> assign) where T : IEquatable<T>
    {
        if (value.Equals(current))
            return;
        assign(value);
        SaveAndNotify();
    }

    private void SaveAndNotify()
    {
        try
        {
            _store.Save(_settings);
        }
        catch
        {
        }

        _sessionSecurity.ApplySettings(_settings.ToSessionSecuritySettings());
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
