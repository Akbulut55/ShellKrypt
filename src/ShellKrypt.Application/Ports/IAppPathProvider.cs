namespace ShellKrypt.Application.Ports;

public interface IAppPathProvider
{
    string AppDataDirectory { get; }
    string SuggestedVaultDirectory { get; }
    string GetSuggestedVaultPath(string displayName);
}
