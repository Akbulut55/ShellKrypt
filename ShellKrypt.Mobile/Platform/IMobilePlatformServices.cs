namespace ShellKrypt.Mobile.Platform;

public interface IMobilePlatformServices
{
    string PlatformName { get; }

    MobilePlatformCapabilities Capabilities { get; }

    Task SetClipboardTextAsync(string text, CancellationToken cancellationToken = default);

    Task<MobileClipboardClearResult> ClearClipboardAsync(CancellationToken cancellationToken = default);

    Task<byte[]?> PickImageAsync(CancellationToken cancellationToken = default);

    Task<string?> PickOpenFileAsync(MobileFilePickerOptions options, CancellationToken cancellationToken = default);

    Task<string?> PickSaveFileAsync(string suggestedFileName, CancellationToken cancellationToken = default);

    Task ShareFileAsync(MobileShareRequest request, CancellationToken cancellationToken = default);

    Task<string?> GetSecureSettingAsync(string key, CancellationToken cancellationToken = default);

    Task SetSecureSettingAsync(string key, string? value, CancellationToken cancellationToken = default);

    Task SetPrivacyScreenAsync(bool enabled, CancellationToken cancellationToken = default);

    Task<MobileBiometricResult> TryUnlockWithBiometricsAsync(string reason, CancellationToken cancellationToken = default);
}

public sealed record MobilePlatformCapabilities(
    ClipboardClearSupport ClipboardClearSupport,
    bool SupportsSecureStorage,
    bool SupportsFilePicker,
    bool SupportsShareSheet,
    bool SupportsQrImagePicker,
    bool SupportsPrivacyScreen,
    bool SupportsBiometricUnlock);

public sealed record MobileFilePickerOptions(string Title, IReadOnlyList<string> AllowedExtensions);

public sealed record MobileShareRequest(string FilePath, string Title, string? MimeType = null);

public sealed record MobileClipboardClearResult(bool WasRequested, bool IsConfirmed, string Message);

public sealed record MobileBiometricResult(bool IsAvailable, bool IsAuthenticated, string Message);

public enum ClipboardClearSupport
{
    Unsupported,
    BestEffort,
    Supported
}
