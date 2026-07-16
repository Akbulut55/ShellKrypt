using System.IO;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void SetVaultPath(string path)
    {
        var nextPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        if (!string.Equals(_vaultSession.VaultPath, nextPath, StringComparison.OrdinalIgnoreCase))
        {
            _ = _secureClipboard.ClearAsync();
            _automaticBackups.ClearSessionPassphrase();
        }
        _vaultSession.SetVaultPath(nextPath);
    }

    public void RecordActivity() => _sessionSecurity.RecordActivity();
    public void HandleWindowActivated() => _sessionSecurity.HandleWindowActivated();
    public void HandleWindowDeactivated() => _sessionSecurity.HandleWindowDeactivated();
    public void GoWelcome() => _navigation.GoWelcome();
    public void GoCreateVault() => _navigation.GoCreateVault();
    public void GoUnlock() => _navigation.GoUnlock();
    public void OnUnlocked(byte[] vaultKey) => _navigation.OnUnlocked(vaultKey);
    public void Lock() => _navigation.Lock();
    public void ReloadShell() => _navigation.ReloadShell();
}
