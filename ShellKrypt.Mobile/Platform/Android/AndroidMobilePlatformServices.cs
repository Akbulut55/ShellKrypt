namespace ShellKrypt.Mobile.Platform.Android;

public sealed class AndroidMobilePlatformServices()
    : UnsupportedMobilePlatformServices("Android")
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
