using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void AttachClipboard(IClipboard? clipboard) => _secureClipboard.Attach(clipboard);
    public Task CopyToClipboardAsync(string text) => _secureClipboard.CopyAsync(text);
    public Task ClearClipboardAsync() => _secureClipboard.ClearAsync();
    public Task<Bitmap?> TryGetClipboardBitmapAsync() => _secureClipboard.TryGetBitmapAsync();
}
