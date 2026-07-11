using ShellKrypt.Infrastructure.CryptoTools;
using Xunit;

namespace ShellKrypt.Tests.CryptoTools;

public sealed class Base64ServiceTests
{
    private readonly Base64Service _service = new();

    [Theory]
    [InlineData("abc")]
    [InlineData("ShellKrypt şifreli kasa")]
    public void EncodeAndDecodeBase64_RoundTripsUtf8(string input)
    {
        var encoded = _service.EncodeBase64(input);

        Assert.Equal(input, _service.DecodeBase64(encoded));
    }

    [Fact]
    public void DecodeBase64_ReturnsEmpty_ForMalformedInput()
    {
        Assert.Empty(_service.DecodeBase64("not valid base64"));
    }

    [Fact]
    public void EncodeAndDecodeBase64_ReturnEmpty_ForEmptyInput()
    {
        Assert.Empty(_service.EncodeBase64(""));
        Assert.Empty(_service.DecodeBase64(""));
    }
}
