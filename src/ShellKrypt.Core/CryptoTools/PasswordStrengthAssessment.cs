namespace ShellKrypt.Core.CryptoTools;

public enum PasswordStrengthRating
{
    None,
    Weak,
    Fair,
    Strong
}

public sealed record PasswordStrengthAssessment(
    double EntropyBits,
    int Score,
    PasswordStrengthRating Rating);
