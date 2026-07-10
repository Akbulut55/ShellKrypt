namespace ShellKrypt.Core.CryptoTools;

public sealed record PasswordGenerationOptions(
    int Length,
    bool IncludeLowercase,
    bool IncludeUppercase,
    bool IncludeNumbers,
    bool IncludeSymbols);
