namespace ShellKrypt.Mobile.Security;

public sealed record MobileSecuritySettings(
    bool LockOnBackground,
    bool EnablePrivacyScreen,
    bool WarnBeforeCopy,
    int ClipboardClearSeconds,
    bool AllowBiometricUnlock)
{
    public const int MinimumClipboardClearSeconds = 5;
    public const int DefaultClipboardClearSeconds = 30;

    public static MobileSecuritySettings Default { get; } = new(
        LockOnBackground: true,
        EnablePrivacyScreen: true,
        WarnBeforeCopy: true,
        ClipboardClearSeconds: DefaultClipboardClearSeconds,
        AllowBiometricUnlock: false);

    public MobileSecuritySettings Normalize()
        => this with
        {
            ClipboardClearSeconds = Math.Max(MinimumClipboardClearSeconds, ClipboardClearSeconds)
        };

    public string ClipboardBoundaryText =>
        "Clipboard clearing is best-effort and is not a security boundary.";

    public string BiometricBoundaryText =>
        "Biometric unlock is optional convenience only. The master password remains required and is never recoverable.";
}
