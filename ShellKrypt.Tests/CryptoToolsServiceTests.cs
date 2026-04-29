using ShellKrypt.Core.Tools;
using ShellKrypt.Infrastructure.Tools;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class CryptoToolsServiceTests
{
    [Fact]
    public void GeneratePassword_UsesSelectedPoolsAndLength()
    {
        var service = new CryptoToolsService();

        var password = service.GeneratePassword(new PasswordGenerationOptions(
            Length: 32,
            IncludeLowercase: false,
            IncludeUppercase: false,
            IncludeNumbers: true,
            IncludeSymbols: false));

        Assert.NotNull(password);
        Assert.Equal(32, password!.Length);
        Assert.All(password, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void GeneratePassword_ReturnsNull_WhenNoPoolsAreSelected()
    {
        var service = new CryptoToolsService();

        var password = service.GeneratePassword(new PasswordGenerationOptions(
            Length: 32,
            IncludeLowercase: false,
            IncludeUppercase: false,
            IncludeNumbers: false,
            IncludeSymbols: false));

        Assert.Null(password);
    }

    [Fact]
    public void HashAndBase64Helpers_MatchExpectedOutput()
    {
        var service = new CryptoToolsService();

        Assert.Equal(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            service.ComputeSha256("abc"));
        Assert.Equal("YWJj", service.EncodeBase64("abc"));
        Assert.Equal("abc", service.DecodeBase64("YWJj"));
        Assert.Equal("", service.DecodeBase64("not valid base64"));
    }

    [Fact]
    public void AssessPasswordStrength_ScoresComplexPasswordHigherThanWeakPassword()
    {
        var service = new CryptoToolsService();

        var weak = service.AssessPasswordStrength("aaaaaaa");
        var strong = service.AssessPasswordStrength("b0V.wGgU[LeCm1H&F&o}GXInN-T4HFM/");

        Assert.Equal(PasswordStrengthRating.Weak, weak.Rating);
        Assert.True(strong.Score > weak.Score);
        Assert.True(strong.Rating is PasswordStrengthRating.Strong or PasswordStrengthRating.Secure);
    }
}
