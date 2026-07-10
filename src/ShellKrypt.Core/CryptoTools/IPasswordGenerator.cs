namespace ShellKrypt.Core.CryptoTools;

public interface IPasswordGenerator
{
    string? GeneratePassword(PasswordGenerationOptions options);
}
