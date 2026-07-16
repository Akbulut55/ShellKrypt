using ShellKrypt.Desktop.Features.BackupCenter;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void SetAutomaticBackupSessionPassphrase(string? passphrase)
        => _automaticBackups.SetSessionPassphrase(passphrase);

    public void ClearAutomaticBackupSessionPassphrase()
        => _automaticBackups.ClearSessionPassphrase();

    public Task<AutomaticBackupRunResult> RunAutomaticBackupNowAsync()
        => _automaticBackups.RunNowAsync();
}
