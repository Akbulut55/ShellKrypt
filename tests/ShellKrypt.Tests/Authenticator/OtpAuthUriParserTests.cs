using ShellKrypt.Core.Authenticator;
using ShellKrypt.Infrastructure.Authenticator;
using Xunit;

namespace ShellKrypt.Tests.Authenticator;

public sealed class OtpAuthUriParserTests
{
    private readonly OtpAuthUriParser _parser = new();

    [Theory]
    [InlineData("otpauth://totp/GitHub:octocat@example.com?secret=JBSWY3DPEHPK3PXP&issuer=GitHub", "GitHub", "JBSWY3DPEHPK3PXP", AuthenticatorKeyType.TimeBased, 0, "HMAC-SHA1", 6, 30)]
    [InlineData("otpauth://hotp/Build%20Server?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&counter=7", "Build Server", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", AuthenticatorKeyType.CounterBased, 7, "HMAC-SHA1", 6, 30)]
    [InlineData("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&algorithm=SHA512&digits=8&period=45", "Example", "JBSWY3DPEHPK3PXP", AuthenticatorKeyType.TimeBased, 0, "HMAC-SHA512", 8, 45)]
    public void Parse_ReadsExpectedFields(string uri, string name, string secret, AuthenticatorKeyType keyType, long counter, string algorithm, int digits, int period)
    {
        var parsed = _parser.Parse(uri);

        Assert.Equal(name, parsed.Name);
        Assert.Equal(secret, parsed.Secret);
        Assert.Equal(keyType, parsed.KeyType);
        Assert.Equal(counter, parsed.Counter);
        Assert.Equal(algorithm, parsed.Algorithm);
        Assert.Equal(digits, parsed.Digits);
        Assert.Equal(period, parsed.PeriodSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("otpauth://steam/example?secret=ABC")]
    public void Parse_RejectsUnsupportedInput(string value)
        => Assert.Throws<InvalidOperationException>(() => _parser.Parse(value));
}
