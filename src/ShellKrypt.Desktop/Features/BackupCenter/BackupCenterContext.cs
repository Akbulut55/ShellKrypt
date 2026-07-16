using ShellKrypt.Application.Backups;
using ShellKrypt.Application.Localization;
using ShellKrypt.Core.Backups;
using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Desktop.Services.Runtime;

namespace ShellKrypt.Desktop.Features.BackupCenter;

internal sealed class BackupCenterContext(
    DesktopFeatureServices desktop,
    IAutomaticBackupController automaticBackups,
    IEncryptedVaultBackupService backups,
    IVaultPlaintextExportService plaintextExports,
    IVaultCsvImportService csvImports,
    IDesktopNavigation navigation)
{
    public LocalizationService Localization => desktop.Localization;
    public IEncryptedVaultBackupService Backups => backups;
    public IVaultPlaintextExportService PlaintextExports => plaintextExports;
    public IVaultCsvImportService CsvImports => csvImports;
    public BackupCenterHistory History => automaticBackups.History;
    public BackupScheduleSettings Schedule => automaticBackups.Schedule;
    public AutomaticBackupState AutomaticState => automaticBackups.State;
    public string? VaultPath => desktop.Session.VaultPath;
    public bool HasAutomaticBackupPassphrase => automaticBackups.HasSessionPassphrase;
    public bool IsAutomaticBackupRunning => automaticBackups.IsRunning;

    public event EventHandler? AutomaticBackupChanged
    {
        add => automaticBackups.Changed += value;
        remove => automaticBackups.Changed -= value;
    }

    public string T(string key, params object[] args) => Localization.Get(key, args);

    public bool TryGetUnlockedVault(BackupOperationState operation, out string vaultPath, out byte[] vaultKey)
    {
        vaultPath = "";
        vaultKey = [];
        if (!desktop.Session.IsUnlocked || string.IsNullOrWhiteSpace(desktop.Session.VaultPath))
        {
            operation.Status = T("BackupCenter.Status.UnlockVault");
            return false;
        }

        vaultPath = desktop.Session.VaultPath;
        vaultKey = desktop.Session.VaultKey;
        return true;
    }

    public string VaultDisplayName => string.IsNullOrWhiteSpace(desktop.Session.VaultPath)
        ? T("BackupCenter.Status.NoActiveVault")
        : Path.GetFileNameWithoutExtension(desktop.Session.VaultPath);

    public Task<string?> PickOpenFileAsync(string title, string[] extensions, string fileTypeName)
        => desktop.Dialogs.PickOpenFileAsync(title, extensions, fileTypeName);
    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string defaultExtension, string[] extensions, string fileTypeName)
        => desktop.Dialogs.PickSaveFileAsync(title, suggestedName, defaultExtension, extensions, fileTypeName);
    public Task<string?> PickFolderAsync(string title) => desktop.Dialogs.PickFolderAsync(title);
    public Task ClearClipboardAsync() => desktop.Clipboard.ClearAsync();
    public void SetAutomaticBackupPassphrase(string? value) => automaticBackups.SetSessionPassphrase(value);
    public void ClearAutomaticBackupPassphrase() => automaticBackups.ClearSessionPassphrase();
    public Task<AutomaticBackupRunResult> RunAutomaticBackupNowAsync() => automaticBackups.RunNowAsync();
    public void SaveSchedule() => automaticBackups.SaveSchedule();
    public void SaveHistory() => automaticBackups.SaveHistory();
    public void ReloadShell() => navigation.ReloadShell();
    public void LogActivity(string category, string title, string detail, string severity, string? vaultPath, string? affectedItem)
        => desktop.Activity.Log(category, title, detail, severity, vaultPath, affectedItem);
}
