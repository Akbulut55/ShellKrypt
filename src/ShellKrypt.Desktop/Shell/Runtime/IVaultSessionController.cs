namespace ShellKrypt.Desktop.Shell.Runtime;

public interface IVaultSessionController
{
    string? VaultPath { get; }
    bool IsUnlocked { get; }
    byte[] VaultKey { get; }
    event EventHandler? StateChanged;
    void SetVaultPath(string? path);
    void SetVaultKey(byte[] vaultKey);
    void ClearSensitive();
}
