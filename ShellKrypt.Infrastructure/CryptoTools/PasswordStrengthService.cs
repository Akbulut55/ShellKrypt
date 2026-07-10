using ShellKrypt.Core.CryptoTools;

namespace ShellKrypt.Infrastructure.CryptoTools;

public sealed class PasswordStrengthService : IPasswordStrengthService
{
    public PasswordStrengthAssessment AssessPasswordStrength(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return new PasswordStrengthAssessment(0, 0, PasswordStrengthRating.None);

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigits = password.Any(char.IsDigit);
        var hasSymbols = password.Any(ch => !char.IsLetterOrDigit(ch));

        var poolSize = 0;
        var enabledClasses = 0;

        if (hasLower)
        {
            poolSize += PasswordCharacterSets.Lowercase.Length;
            enabledClasses++;
        }

        if (hasUpper)
        {
            poolSize += PasswordCharacterSets.Uppercase.Length;
            enabledClasses++;
        }

        if (hasDigits)
        {
            poolSize += PasswordCharacterSets.Numbers.Length;
            enabledClasses++;
        }

        if (hasSymbols)
        {
            poolSize += PasswordCharacterSets.Symbols.Length;
            enabledClasses++;
        }

        if (poolSize == 0)
            return new PasswordStrengthAssessment(0, 0, PasswordStrengthRating.None);

        var entropyBits = password.Length * Math.Log2(poolSize);
        var lengthScore = Math.Clamp(password.Length / 20.0 * 40.0, 0.0, 40.0);
        var diversityScore = enabledClasses / 4.0 * 25.0;
        var entropyScore = Math.Clamp(entropyBits / 110.0 * 35.0, 0.0, 35.0);
        var score = (int)Math.Clamp(Math.Round(lengthScore + diversityScore + entropyScore), 0, 100);

        var rating = score switch
        {
            >= 60 => PasswordStrengthRating.Strong,
            >= 35 => PasswordStrengthRating.Fair,
            _ => PasswordStrengthRating.Weak
        };

        return new PasswordStrengthAssessment(entropyBits, score, rating);
    }
}
