using System;

namespace ShellKrypt.Desktop.Services;

public sealed record SessionSecuritySettings
{
    public const int MinClipboardClearSeconds = 5;

    public bool AutoLockEnabled { get; init; } = true;
    public int AutoLockMinutes { get; init; } = 15;
    public bool LockOnDeactivate { get; init; }
    public int LockOnDeactivateSeconds { get; init; } = 20;
    public int ClipboardClearSeconds { get; init; } = 15;
    public bool ClipboardCopyEnabled { get; init; } = true;

    public SessionSecuritySettings Normalize()
    {
        return this with
        {
            AutoLockMinutes = Math.Max(1, AutoLockMinutes),
            LockOnDeactivateSeconds = Math.Max(1, LockOnDeactivateSeconds),
            ClipboardClearSeconds = Math.Max(MinClipboardClearSeconds, ClipboardClearSeconds)
        };
    }
}
