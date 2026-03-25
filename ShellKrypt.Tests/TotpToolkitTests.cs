using ShellKrypt.Core.Items;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class TotpToolkitTests
{
    [Fact]
    public void GenerateCode_SupportsStandardOtpauthUri()
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(59);
        var input = "otpauth://totp/ShellKrypt:alice@example.com?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&algorithm=SHA1&digits=8&period=30";

        var code = TotpToolkit.GenerateCode(input, timestamp);

        Assert.Equal("94287082", code);
    }

    [Fact]
    public void TryGenerateCode_ReturnsCountdownForRawBase32Secret()
    {
        var timestamp = DateTimeOffset.FromUnixTimeSeconds(59);

        var ok = TotpToolkit.TryGenerateCode(
            "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
            timestamp,
            out var code,
            out var secondsRemaining,
            out var error);

        Assert.True(ok);
        Assert.Equal("287082", code);
        Assert.Equal(1, secondsRemaining);
        Assert.Equal("", error);
    }

    [Fact]
    public void TryParse_RejectsInvalidSecretCharacters()
    {
        var ok = TotpToolkit.TryParse("BAD-SECRET-1", out var config, out var error);

        Assert.False(ok);
        Assert.Null(config);
        Assert.Contains("base32", error, StringComparison.OrdinalIgnoreCase);
    }
}
