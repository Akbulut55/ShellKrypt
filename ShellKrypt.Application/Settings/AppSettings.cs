using System.Text.Json.Serialization;

namespace ShellKrypt.Application.Settings;

public sealed class AppSettings
{
    public const string DefaultThemeId = "dark";
    public const int CurrentSecurityAcknowledgementVersion = 1;

    public static readonly IReadOnlySet<string> KnownThemeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "dark",
        "light",
        "crimson",
        "ocean",
        "forest"
    };

    public string ThemeId { get; set; } = DefaultThemeId;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Dark;

    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 15;
    public bool LockOnDeactivate { get; set; }
    public int LockOnDeactivateSeconds { get; set; } = 20;
    public int ClipboardClearSeconds { get; set; } = 15;
    public bool ClipboardCopyEnabled { get; set; } = true;
    public string? SecurityAcknowledgementAcceptedAtUtc { get; set; }
    public int SecurityAcknowledgementVersionAccepted { get; set; }

    [JsonIgnore]
    public bool HasCurrentSecurityAcknowledgement =>
        !string.IsNullOrWhiteSpace(SecurityAcknowledgementAcceptedAtUtc) &&
        SecurityAcknowledgementVersionAccepted >= CurrentSecurityAcknowledgementVersion;

    public void AcceptCurrentSecurityAcknowledgement(string acceptedAtUtc)
    {
        SecurityAcknowledgementAcceptedAtUtc = acceptedAtUtc;
        SecurityAcknowledgementVersionAccepted = CurrentSecurityAcknowledgementVersion;
    }

    public void NormalizeThemeId()
    {
        ThemeId = NormalizeThemeId(ThemeId);
        ThemeMode = AppThemeMode.Dark;
    }

    public static string NormalizeThemeId(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
            return DefaultThemeId;

        var normalized = themeId.Trim().ToLowerInvariant();
        return KnownThemeIds.Contains(normalized) ? normalized : DefaultThemeId;
    }

    public static string ThemeIdFromLegacyMode(AppThemeMode mode)
        => mode == AppThemeMode.Light ? "light" : DefaultThemeId;

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
