namespace ShellKrypt.Core.Tools;

public sealed record PasswordGenerationOptions(
    int Length,
    bool IncludeLowercase,
    bool IncludeUppercase,
    bool IncludeNumbers,
    bool IncludeSymbols);

public interface ICryptoToolsService
{
    string? GeneratePassword(PasswordGenerationOptions options);
    string ComputeSha256(string input);
    string ComputeSha512(string input);
    string EncodeBase64(string input);
    string DecodeBase64(string input);
}
