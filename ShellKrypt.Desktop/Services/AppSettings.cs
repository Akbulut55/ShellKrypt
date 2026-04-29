namespace ShellKrypt.Desktop.Services;

public sealed class AppSettings
{
    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 15;
    public bool LockOnDeactivate { get; set; } = false;
    public int LockOnDeactivateSeconds { get; set; } = 20;
    public int ClipboardClearSeconds { get; set; } = 15;
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Dark;

    public SessionSecuritySettings ToSessionSecuritySettings()
    {
        return new SessionSecuritySettings
        {
            AutoLockEnabled = AutoLockEnabled,
            AutoLockMinutes = AutoLockMinutes,
            LockOnDeactivate = LockOnDeactivate,
            LockOnDeactivateSeconds = LockOnDeactivateSeconds,
            ClipboardClearSeconds = ClipboardClearSeconds
        }.Normalize();
    }

    public void ApplySessionSecuritySettings(SessionSecuritySettings settings)
    {
        var normalized = settings.Normalize();
        AutoLockEnabled = normalized.AutoLockEnabled;
        AutoLockMinutes = normalized.AutoLockMinutes;
        LockOnDeactivate = normalized.LockOnDeactivate;
        LockOnDeactivateSeconds = normalized.LockOnDeactivateSeconds;
        ClipboardClearSeconds = normalized.ClipboardClearSeconds;
    }
}
