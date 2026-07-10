using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Infrastructure.CryptoTools;
using Xunit;

namespace ShellKrypt.Tests.CryptoTools;

public sealed class PasswordStrengthServiceTests
{
    private readonly PasswordStrengthService _service = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AssessPasswordStrength_ReturnsNone_ForEmptyInput(string? password)
    {
        var assessment = _service.AssessPasswordStrength(password);

        Assert.Equal(0, assessment.Score);
        Assert.Equal(0, assessment.EntropyBits);
        Assert.Equal(PasswordStrengthRating.None, assessment.Rating);
    }

    [Theory]
    [InlineData("aaaaaaa", PasswordStrengthRating.Weak)]
    [InlineData("Password1", PasswordStrengthRating.Fair)]
    [InlineData("b0V.wGgU[LeCm1H&F&o}GXInN-T4HFM/", PasswordStrengthRating.Strong)]
    public void AssessPasswordStrength_ReturnsExpectedRating(
        string password,
        PasswordStrengthRating expectedRating)
    {
        var assessment = _service.AssessPasswordStrength(password);

        Assert.Equal(expectedRating, assessment.Rating);
        Assert.InRange(assessment.Score, 0, 100);
    }
}
