namespace ShellKrypt.Application.Settings;

public sealed class AppSettings
{
    public const int CurrentSecurityAcknowledgementVersion = 1;

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Dark;
    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 15;
    public bool LockOnDeactivate { get; set; }
    public int LockOnDeactivateSeconds { get; set; } = 20;
    public int ClipboardClearSeconds { get; set; } = 15;
    public bool ClipboardCopyEnabled { get; set; } = true;
    public string? SecurityAcknowledgementAcceptedAtUtc { get; set; }
    public int SecurityAcknowledgementVersionAccepted { get; set; }

    public bool HasCurrentSecurityAcknowledgement =>
        !string.IsNullOrWhiteSpace(SecurityAcknowledgementAcceptedAtUtc) &&
        SecurityAcknowledgementVersionAccepted >= CurrentSecurityAcknowledgementVersion;

    public void AcceptCurrentSecurityAcknowledgement(string acceptedAtUtc)
    {
        SecurityAcknowledgementAcceptedAtUtc = acceptedAtUtc;
        SecurityAcknowledgementVersionAccepted = CurrentSecurityAcknowledgementVersion;
    }

    public SessionSecuritySettings ToSessionSecuritySettings()
    {
        return new SessionSecuritySettings
        {
            AutoLockEnabled = AutoLockEnabled,
            AutoLockMinutes = AutoLockMinutes,
            LockOnDeactivate = LockOnDeactivate,
            LockOnDeactivateSeconds = LockOnDeactivateSeconds,
            ClipboardClearSeconds = ClipboardClearSeconds,
            ClipboardCopyEnabled = ClipboardCopyEnabled
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
        ClipboardCopyEnabled = normalized.ClipboardCopyEnabled;
    }
}
