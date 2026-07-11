namespace ShellKrypt.Mobile.Platform.iOS;

public sealed class IosMobilePlatformServices()
    : UnsupportedMobilePlatformServices("iOS")
{
    public override MobilePlatformCapabilities Capabilities { get; } = new(
        ClipboardClearSupport.BestEffort,
        SupportsSecureStorage: true,
        SupportsFilePicker: true,
        SupportsShareSheet: true,
        SupportsQrImagePicker: true,
        SupportsPrivacyScreen: true,
        SupportsBiometricUnlock: true);
}
