namespace ShellKrypt.Core.Tools;

public sealed record PasswordGenerationOptions(
    int Length,
    bool IncludeLowercase,
    bool IncludeUppercase,
    bool IncludeNumbers,
    bool IncludeSymbols);

public enum PasswordStrengthRating
{
    None,
    Weak,
    Fair,
    Strong,
    Secure
}

public sealed record PasswordStrengthAssessment(
    double EntropyBits,
    int Score,
    PasswordStrengthRating Rating);

public interface ICryptoToolsService
{
    string? GeneratePassword(PasswordGenerationOptions options);
    PasswordStrengthAssessment AssessPasswordStrength(string? password);
    string ComputeSha256(string input);
    string ComputeSha512(string input);
    string EncodeBase64(string input);
    string DecodeBase64(string input);
}
