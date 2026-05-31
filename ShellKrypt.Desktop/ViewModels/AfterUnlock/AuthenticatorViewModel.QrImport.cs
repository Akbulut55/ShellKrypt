using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class AuthenticatorViewModel
{
    [RelayCommand]
    private async Task ImportQrScreenshotAsync()
    {
        Error = string.Empty;

        var path = await _root.PickOpenFileAsync(
            "Select QR screenshot",
            [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"],
            "Image File");

        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            ApplyImportedSecret(_qrImportService.ImportFromImage(path));
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PasteQrImageAsync()
    {
        Error = string.Empty;

        try
        {
            var bitmap = await _root.TryGetClipboardBitmapAsync();
            if (bitmap is null)
            {
                Error = "Clipboard does not contain an image to scan.";
                return;
            }

            using (bitmap)
            {
                ApplyImportedSecret(_qrImportService.ImportFromBitmap(bitmap));
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
