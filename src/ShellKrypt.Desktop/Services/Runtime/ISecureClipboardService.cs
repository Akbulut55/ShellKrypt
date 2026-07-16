using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace ShellKrypt.Desktop.Services.Runtime;

public interface ISecureClipboardService
{
    void Attach(IClipboard? clipboard);
    Task CopyAsync(string text);
    Task ClearAsync();
    Task<Bitmap?> TryGetBitmapAsync();
}
