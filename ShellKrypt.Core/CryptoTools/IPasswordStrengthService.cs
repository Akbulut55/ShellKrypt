namespace ShellKrypt.Core.CryptoTools;

public interface IPasswordStrengthService
{
    PasswordStrengthAssessment AssessPasswordStrength(string? password);
}
