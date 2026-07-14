using ShellKrypt.Core.Authenticator;
using ShellKrypt.Infrastructure.Authenticator;
using Xunit;

namespace ShellKrypt.Tests.Authenticator;

public sealed class OneTimePasswordGeneratorTests
{
    private readonly OneTimePasswordGenerator _generator = new();

    [Fact]
    public void GetCurrentCode_UsesRfc6238TotpVector()
    {
        var snapshot = _generator.GetCurrentCode(CreateEntry(AuthenticatorKeyType.TimeBased, digits: 8), DateTimeOffset.FromUnixTimeSeconds(59));

        Assert.True(snapshot.IsValid);
        Assert.Equal("94287082", snapshot.Code);
        Assert.Equal(1, snapshot.SecondsRemaining);
    }

    [Fact]
    public void GetCurrentCode_UsesRfc4226HotpVector()
    {
        var snapshot = _generator.GetCurrentCode(CreateEntry(AuthenticatorKeyType.CounterBased, digits: 6));

        Assert.True(snapshot.IsValid);
        Assert.Equal("755224", snapshot.Code);
        Assert.Equal(0, snapshot.SecondsRemaining);
    }

    [Fact]
    public void GetCurrentCode_ReturnsInvalidSnapshotForMalformedSecret()
    {
        var entry = CreateEntry(AuthenticatorKeyType.TimeBased, digits: 6) with { Secret = "not-base32!" };

        var snapshot = _generator.GetCurrentCode(entry);

        Assert.False(snapshot.IsValid);
        Assert.Equal("------", snapshot.Code);
    }

    private static AuthenticatorEntry CreateEntry(AuthenticatorKeyType keyType, int digits)
        => new(
            "auth-1",
            "RFC vector",
            "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
            keyType,
            0,
            "HMAC-SHA1",
            digits,
            30,
            string.Empty,
            DateTimeOffset.UtcNow.ToString("O"),
            DateTimeOffset.UtcNow.ToString("O"));
}
