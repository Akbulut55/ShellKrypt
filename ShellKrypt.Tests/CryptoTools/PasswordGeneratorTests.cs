using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Infrastructure.CryptoTools;
using Xunit;

namespace ShellKrypt.Tests.CryptoTools;

public sealed class PasswordGeneratorTests
{
    private readonly PasswordGenerator _generator = new();

    [Fact]
    public void GeneratePassword_UsesSelectedPoolAndLength()
    {
        var password = _generator.GeneratePassword(new PasswordGenerationOptions(
            Length: 32,
            IncludeLowercase: false,
            IncludeUppercase: false,
            IncludeNumbers: true,
            IncludeSymbols: false));

        Assert.NotNull(password);
        Assert.Equal(32, password!.Length);
        Assert.All(password, character => Assert.True(char.IsDigit(character)));
    }

    [Fact]
    public void GeneratePassword_IncludesEverySelectedCharacterClass()
    {
        var password = _generator.GeneratePassword(new PasswordGenerationOptions(
            Length: 32,
            IncludeLowercase: true,
            IncludeUppercase: true,
            IncludeNumbers: true,
            IncludeSymbols: true));

        Assert.NotNull(password);
        Assert.Contains(password!, char.IsLower);
        Assert.Contains(password!, char.IsUpper);
        Assert.Contains(password!, char.IsDigit);
        Assert.Contains(password!, character => !char.IsLetterOrDigit(character));
    }

    [Fact]
    public void GeneratePassword_ReturnsNull_WhenNoPoolsAreSelected()
    {
        var password = _generator.GeneratePassword(new PasswordGenerationOptions(
            Length: 32,
            IncludeLowercase: false,
            IncludeUppercase: false,
            IncludeNumbers: false,
            IncludeSymbols: false));

        Assert.Null(password);
    }
}
