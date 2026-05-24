namespace ShellKrypt.Application.Settings;

public sealed record SessionSecuritySettings
{
    public const int MinClipboardClearSeconds = 5;
    public const int MinimumClipboardClearSeconds = MinClipboardClearSeconds;
    public const int MinimumAutoLockMinutes = 1;
    public const int MinimumDeactivateLockSeconds = 1;

    public bool AutoLockEnabled { get; init; } = true;
    public int AutoLockMinutes { get; init; } = 15;
    public bool LockOnDeactivate { get; init; }
    public int LockOnDeactivateSeconds { get; init; } = 20;
    public int ClipboardClearSeconds { get; init; } = 15;
    public bool ClipboardCopyEnabled { get; init; } = true;

    public SessionSecuritySettings Normalize() => this with
    {
        AutoLockMinutes = Math.Max(MinimumAutoLockMinutes, AutoLockMinutes),
        LockOnDeactivateSeconds = Math.Max(MinimumDeactivateLockSeconds, LockOnDeactivateSeconds),
        ClipboardClearSeconds = Math.Max(MinClipboardClearSeconds, ClipboardClearSeconds)
    };
}
