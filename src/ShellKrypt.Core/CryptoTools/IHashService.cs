namespace ShellKrypt.Core.CryptoTools;

public interface IHashService
{
    string ComputeSha256(string input);
    string ComputeSha512(string input);
}
