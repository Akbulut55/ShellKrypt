using System;
using System.IO;
using Avalonia.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ShellKrypt.Infrastructure.Items;
using ZXing;
using ZXing.Common;

namespace ShellKrypt.Desktop.Services;

public sealed class AuthenticatorQrImportService
{
    public ParsedOtpAuthSecret ImportFromImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            throw new InvalidOperationException("Select a QR screenshot image first.");

        using var stream = File.OpenRead(imagePath);
        return ImportFromStream(stream, "No QR code could be read from that screenshot.");
    }

    public ParsedOtpAuthSecret ImportFromBitmap(Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        return ImportFromStream(stream, "No QR code could be read from the pasted image.");
    }

    private static ParsedOtpAuthSecret ImportFromStream(Stream stream, string missingQrMessage)
    {
        using var image = Image.Load<Rgba32>(stream);
        var pixelData = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixelData);

        var source = new RGBLuminanceSource(pixelData, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true
            }
        };

        var result = reader.Decode(source);
        if (result is null || string.IsNullOrWhiteSpace(result.Text))
            throw new InvalidOperationException(missingQrMessage);

        return OtpAuthUriParser.Parse(result.Text);
    }
}
