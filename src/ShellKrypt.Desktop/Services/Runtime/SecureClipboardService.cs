using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.Services.Runtime;

public sealed class SecureClipboardService(ClipboardService clipboard, SessionSecurityService sessionSecurity) : ISecureClipboardService
{
    public void Attach(IClipboard? value) => clipboard.Attach(value);

    public Task CopyAsync(string text)
        => sessionSecurity.Settings.ClipboardCopyEnabled
            ? clipboard.CopyAsync(text, sessionSecurity.ClipboardClearDelay)
            : Task.CompletedTask;

    public Task ClearAsync() => clipboard.ClearAsync();

    public Task<Bitmap?> TryGetBitmapAsync() => clipboard.TryGetBitmapAsync();
}
