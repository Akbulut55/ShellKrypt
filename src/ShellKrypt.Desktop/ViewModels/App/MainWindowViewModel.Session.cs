using System;
using System.IO;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void SetVaultPath(string path)
    {
        var nextPath = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
        if (!string.Equals(_state.VaultPath, nextPath, StringComparison.OrdinalIgnoreCase))
        {
            _ = _clipboardService.ClearAsync();
            _automaticBackupCoordinator.ClearSessionPassphrase();
        }

        _state.VaultPath = nextPath;
    }

    public void RecordActivity() => _sessionSecurity.RecordActivity();

    public void HandleWindowActivated() => _sessionSecurity.HandleWindowActivated();

    public void HandleWindowDeactivated() => _sessionSecurity.HandleWindowDeactivated();

    public void GoWelcome() => ReplaceCurrent(new WelcomeViewModel(this, _vaultRegistryService));
    public void GoCreateVault() => ReplaceCurrent(new CreateVaultViewModel(this, _vaultService, _vaultRegistryService));
    public void GoUnlock() => ReplaceCurrent(new UnlockViewModel(this, _vaultService, _vaultRegistryService));

    public void OnUnlocked(byte[] vaultKey)
    {
        _state.VaultKey = vaultKey;
        OnPropertyChanged(nameof(IsUnlocked));

        if (!string.IsNullOrWhiteSpace(_state.VaultPath))
            _vaultRegistryService.MarkOpened(_state.VaultPath);

        LogActivity(
            category: "vault",
            title: "Vault unlocked",
            detail: $"Opened {GetVaultDisplayName(_state.VaultPath)}.",
            severity: "success",
            vaultPath: _state.VaultPath,
            affectedItem: GetVaultDisplayName(_state.VaultPath));

        _sessionSecurity.SetUnlocked(true);
        ReplaceCurrent(CreateShell());
        _automaticBackupCoordinator.Start();
    }

    public void Lock()
    {
        var vaultPath = _state.VaultPath;
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

        _automaticBackupCoordinator.Stop();
        _automaticBackupCoordinator.ClearSessionPassphrase();
        _sessionSecurity.SetUnlocked(false);
        _ = _clipboardService.ClearAsync();
        _state.ClearSensitive();
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

    private ShellViewModel CreateShell()
        => new(
            this,
            _vaultItemSummaryService,
            _webLoginService,
            _cardService,
            _noteService,
            _authenticatorEntryService,
            _oneTimePasswordGenerator,
            _apiKeyService,
            _projectSecretService,
            _quickFillEntryService,
            _authenticatorQrImportService,
            _healthAuditService,
            _passwordGenerator,
            _passwordStrengthService,
            _hashService,
            _base64Service,
            _activityLogService,
            _vaultRegistryService);
}
