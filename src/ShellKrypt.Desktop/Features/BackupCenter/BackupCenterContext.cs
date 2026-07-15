using ShellKrypt.Application.Backups;
using ShellKrypt.Application.Localization;
using ShellKrypt.Core.Backups;
using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Features.BackupCenter;

internal sealed class BackupCenterContext(MainWindowViewModel root)
{
    public LocalizationService Localization => root.Localization;
    public IEncryptedVaultBackupService Backups => root.EncryptedBackupService;
    public IVaultPlaintextExportService PlaintextExports => root.PlaintextExportService;
    public IVaultCsvImportService CsvImports => root.CsvImportService;
    public BackupCenterHistory History => root.BackupCenterHistory;
    public BackupScheduleSettings Schedule => root.BackupSchedule;
    public AutomaticBackupState AutomaticState => root.AutomaticBackupState;
    public string? VaultPath => root.VaultPath;
    public bool HasAutomaticBackupPassphrase => root.AutomaticBackups.HasSessionPassphrase;
    public bool IsAutomaticBackupRunning => root.AutomaticBackups.IsRunning;

    public event EventHandler? AutomaticBackupChanged
    {
        add => root.AutomaticBackupChanged += value;
        remove => root.AutomaticBackupChanged -= value;
    }

    public string T(string key, params object[] args) => Localization.Get(key, args);

    public bool TryGetUnlockedVault(BackupOperationState operation, out string vaultPath, out byte[] vaultKey)
    {
        vaultPath = "";
        vaultKey = [];
        if (!root.IsUnlocked || string.IsNullOrWhiteSpace(root.VaultPath))
        {
            operation.Status = T("BackupCenter.Status.UnlockVault");
            return false;
        }

        vaultPath = root.VaultPath;
        vaultKey = root.VaultKey;
        return true;
    }

    public string VaultDisplayName => string.IsNullOrWhiteSpace(root.VaultPath)
        ? T("BackupCenter.Status.NoActiveVault")
        : Path.GetFileNameWithoutExtension(root.VaultPath);

    public Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName)
        => root.PickOpenFileAsync(title, extensions, fileTypeName);

    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName)
        => root.PickSaveFileAsync(title, suggestedName, defaultExtension, extensions, fileTypeName);

    public Task<string?> PickFolderAsync(string title) => root.PickFolderAsync(title);
    public Task ClearClipboardAsync() => root.ClearClipboardAsync();
    public void SetAutomaticBackupPassphrase(string? value) => root.SetAutomaticBackupSessionPassphrase(value);
    public void ClearAutomaticBackupPassphrase() => root.ClearAutomaticBackupSessionPassphrase();
    public Task<AutomaticBackupRunResult> RunAutomaticBackupNowAsync() => root.RunAutomaticBackupNowAsync();
    public void SaveSchedule() => root.SaveBackupScheduleState();
    public void SaveHistory() => root.SaveBackupCenterHistory();
    public void ReloadShell() => root.ReloadShell();

    public void LogActivity(string category, string title, string detail, string severity, string? vaultPath, string? affectedItem)
        => root.LogActivity(category, title, detail, severity, vaultPath, affectedItem);
}
