using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void AttachClipboard(IClipboard? clipboard) => _clipboardService.Attach(clipboard);

    public async Task CopyToClipboardAsync(string text)
    {
        if (!_sessionSecurity.Settings.ClipboardCopyEnabled)
            return;

        await _clipboardService.CopyAsync(text, _sessionSecurity.ClipboardClearDelay);
    }

    public async Task ClearClipboardAsync()
    {
        await _clipboardService.ClearAsync();
    }

    public async Task<Bitmap?> TryGetClipboardBitmapAsync()
    {
        return await _clipboardService.TryGetBitmapAsync();
    }
}
