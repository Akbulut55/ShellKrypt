namespace ShellKrypt.Mobile.Platform;

public class UnsupportedMobilePlatformServices(string platformName) : IMobilePlatformServices
{
    public string PlatformName { get; } = platformName;

    public virtual MobilePlatformCapabilities Capabilities { get; } = new(
        ClipboardClearSupport.Unsupported,
        SupportsSecureStorage: false,
        SupportsFilePicker: false,
        SupportsShareSheet: false,
        SupportsQrImagePicker: false,
        SupportsPrivacyScreen: false,
        SupportsBiometricUnlock: false);

    public Task SetClipboardTextAsync(string text, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<MobileClipboardClearResult> ClearClipboardAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new MobileClipboardClearResult(
            WasRequested: true,
            IsConfirmed: false,
            Message: "Clipboard clearing is not available in this host."));

    public Task<byte[]?> PickImageAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);

    public Task<string?> PickOpenFileAsync(MobileFilePickerOptions options, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<string?> PickSaveFileAsync(string suggestedFileName, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task ShareFileAsync(MobileShareRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<string?> GetSecureSettingAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task SetSecureSettingAsync(string key, string? value, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SetPrivacyScreenAsync(bool enabled, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<MobileBiometricResult> TryUnlockWithBiometricsAsync(string reason, CancellationToken cancellationToken = default)
        => Task.FromResult(new MobileBiometricResult(
            IsAvailable: false,
            IsAuthenticated: false,
            Message: "Biometric unlock is not available in this host."));
}
