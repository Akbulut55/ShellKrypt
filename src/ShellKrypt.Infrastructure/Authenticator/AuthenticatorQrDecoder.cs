using ShellKrypt.Core.Authenticator;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace ShellKrypt.Infrastructure.Authenticator;

public sealed class AuthenticatorQrDecoder : IAuthenticatorQrDecoder
{
    public const int MaxImagePixels = 16_000_000;

    public string? Decode(Stream imageStream)
    {
        ArgumentNullException.ThrowIfNull(imageStream);

        using var image = Image.Load<Rgba32>(imageStream);
        if ((long)image.Width * image.Height > MaxImagePixels)
            throw new InvalidOperationException("QR screenshot dimensions are too large.");

        var pixelData = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixelData);

        var source = new RGBLuminanceSource(pixelData, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGBA32);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions { TryHarder = true }
        };

        return reader.Decode(source)?.Text;
    }
}
