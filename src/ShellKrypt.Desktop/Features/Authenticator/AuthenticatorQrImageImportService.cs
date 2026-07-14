using System;
using System.IO;
using Avalonia.Media.Imaging;
using ShellKrypt.Application.Authenticator;
using ShellKrypt.Core.Authenticator;

namespace ShellKrypt.Desktop.Features.Authenticator;

public sealed class AuthenticatorQrImageImportService
{
    private const long MaxQrImageBytes = 10L * 1024 * 1024;
    private readonly AuthenticatorQrImportService _importService;

    public AuthenticatorQrImageImportService(AuthenticatorQrImportService importService)
    {
        _importService = importService;
    }

    public ParsedOtpAuthSecret ImportFromImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            throw new InvalidOperationException("Select a QR screenshot image first.");
        if (new FileInfo(imagePath).Length > MaxQrImageBytes)
            throw new InvalidOperationException("QR screenshot image is too large.");

        using var stream = File.OpenRead(imagePath);
        return _importService.Import(stream, "No QR code could be read from that screenshot.");
    }

    public ParsedOtpAuthSecret ImportFromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        if (stream.Length > MaxQrImageBytes)
            throw new InvalidOperationException("Pasted QR image is too large.");

        stream.Position = 0;
        return _importService.Import(stream, "No QR code could be read from the pasted image.");
    }
}
