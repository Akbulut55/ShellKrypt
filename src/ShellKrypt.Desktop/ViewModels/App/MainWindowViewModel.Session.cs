using System;
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

    public void GoWelcome() => ReplaceCurrent(_lockedSurfaces.CreateWelcome(this));
    public void GoCreateVault() => ReplaceCurrent(_lockedSurfaces.CreateCreateVault(this));
    public void GoUnlock() => ReplaceCurrent(_lockedSurfaces.CreateUnlock(this));

    public void OnUnlocked(byte[] vaultKey)
    {
        _vaultSession.SetVaultKey(vaultKey);
        OnPropertyChanged(nameof(IsUnlocked));

        if (!string.IsNullOrWhiteSpace(_vaultSession.VaultPath))
            _vaultRegistryService.MarkOpened(_vaultSession.VaultPath);

        LogActivity(
            category: "vault",
            title: "Vault unlocked",
            detail: $"Opened {GetVaultDisplayName(_vaultSession.VaultPath)}.",
            severity: "success",
            vaultPath: _vaultSession.VaultPath,
            affectedItem: GetVaultDisplayName(_vaultSession.VaultPath));

        _sessionSecurity.SetUnlocked(true);
        ReplaceCurrent(CreateShell());
        _automaticBackups.Start();
    }

    public void Lock()
    {
        var vaultPath = _vaultSession.VaultPath;
        if (!string.IsNullOrWhiteSpace(vaultPath))
        {
            LogActivity(
                category: "vault",
                title: "Vault locked",
                detail: $"Locked {GetVaultDisplayName(vaultPath)}.",
                severity: "info",
                vaultPath: vaultPath,
                affectedItem: GetVaultDisplayName(vaultPath));
        }

        _automaticBackups.Stop();
        _automaticBackups.ClearSessionPassphrase();
        _sessionSecurity.SetUnlocked(false);
        _ = _secureClipboard.ClearAsync();
        _vaultSession.ClearSensitive();
        OnPropertyChanged(nameof(IsUnlocked));
        GoWelcome();
    }

    public void ReloadShell()
    {
        if (!IsUnlocked)
            return;

        ReplaceCurrent(CreateShell());
        _sessionSecurity.RecordActivity();
    }

    private void ReplaceCurrent(ViewModelBase next)
    {
        if (Current is ShellViewModel shell)
            shell.Deactivate();

        Current = next;
    }

    private ShellViewModel CreateShell() => _unlockedWorkspaces.Create(this);
}
