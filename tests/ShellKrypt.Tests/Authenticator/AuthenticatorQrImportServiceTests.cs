using ShellKrypt.Application.Authenticator;
using ShellKrypt.Core.Authenticator;
using Xunit;

namespace ShellKrypt.Tests.Authenticator;

public sealed class AuthenticatorQrImportServiceTests
{
    [Fact]
    public void Import_DecodesThenParsesOtpAuthUri()
    {
        const string uri = "otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP";
        var expected = new ParsedOtpAuthSecret(
            "Example",
            "JBSWY3DPEHPK3PXP",
            AuthenticatorKeyType.TimeBased,
            0,
            "HMAC-SHA1",
            6,
            30);
        var decoder = new StubDecoder(uri);
        var parser = new StubParser(expected);
        var service = new AuthenticatorQrImportService(decoder, parser);

        var result = service.Import(Stream.Null, "No QR code found.");

        Assert.Equal(expected, result);
        Assert.Equal(uri, parser.ReceivedValue);
    }

    [Fact]
    public void Import_UsesProvidedMessageWhenImageHasNoQrCode()
    {
        var service = new AuthenticatorQrImportService(
            new StubDecoder(null),
            new StubParser(null!));

        var error = Assert.Throws<InvalidOperationException>(
            () => service.Import(Stream.Null, "No supported QR code was found."));

        Assert.Equal("No supported QR code was found.", error.Message);
    }

    private sealed class StubDecoder(string? value) : IAuthenticatorQrDecoder
    {
        public string? Decode(Stream imageStream) => value;
    }

    private sealed class StubParser(ParsedOtpAuthSecret result) : IOtpAuthUriParser
    {
        public string? ReceivedValue { get; private set; }

        public ParsedOtpAuthSecret Parse(string otpauthUri)
        {
            ReceivedValue = otpauthUri;
            return result;
        }
    }
}
