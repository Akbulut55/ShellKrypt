namespace ShellKrypt.Core.CryptoTools;

public interface IBase64Service
{
    string EncodeBase64(string input);
    string DecodeBase64(string input);
}
