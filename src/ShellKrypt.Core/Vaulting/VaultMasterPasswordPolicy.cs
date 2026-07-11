using System.Linq;

namespace ShellKrypt.Core.Vaulting;

public sealed record VaultPasswordPolicyResult(bool IsValid, string Message);

public static class VaultMasterPasswordPolicy
{
    public const int MinimumLength = 8;
    public const int LongSecretMinimumLength = 8;
    public const int MinimumPassphraseWords = 3;

    public static string Guidance =>
        "Use at least 8 characters. Recommended: a 3-word passphrase, or an 8+ character secret with uppercase, lowercase, and digits.";

    public static VaultPasswordPolicyResult Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return new VaultPasswordPolicyResult(false, "Master password is required.");

        var value = password.Trim();
        if (value.Length < MinimumLength)
            return new VaultPasswordPolicyResult(false, $"Use at least {MinimumLength} characters.");

        var wordCount = value
            .Split(' ', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
            .Length;

        if (wordCount >= MinimumPassphraseWords)
            return new VaultPasswordPolicyResult(true, "Strong passphrase.");

        var hasLower = value.Any(char.IsLower);
        var hasUpper = value.Any(char.IsUpper);
        var hasDigit = value.Any(char.IsDigit);

        if (value.Length >= LongSecretMinimumLength && hasLower && hasUpper && hasDigit)
            return new VaultPasswordPolicyResult(true, "Strong long-form secret.");

        return new VaultPasswordPolicyResult(
            false,
            $"Use at least {MinimumLength} characters and either a {MinimumPassphraseWords}-word passphrase or a mixed secret with uppercase, lowercase, and digits.");
    }
}
