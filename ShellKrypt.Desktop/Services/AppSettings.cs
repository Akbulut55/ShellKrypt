namespace ShellKrypt.Desktop.Services;

public sealed class AppSettings
{
    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 15;
    public bool LockOnDeactivate { get; set; } = true;
    public int ClipboardClearSeconds { get; set; } = 15;
}
