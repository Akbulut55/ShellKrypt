using System;
using System.IO;
using System.Threading.Tasks;
using ShellKrypt.Application.Backups;
using ShellKrypt.Desktop.Features.BackupCenter;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel
{
    public void SetAutomaticBackupSessionPassphrase(string? passphrase)
        => _automaticBackupCoordinator.SetSessionPassphrase(passphrase);

    public void ClearAutomaticBackupSessionPassphrase()
        => _automaticBackupCoordinator.ClearSessionPassphrase();

    public async Task<AutomaticBackupRunResult> RunAutomaticBackupNowAsync()
    {
        var result = await _automaticBackupCoordinator.RunNowAsync();
        RecordAutomaticBackupResult(result);
        SaveBackupScheduleState();
        return result;
    }

    internal AutomaticBackupContext? BuildAutomaticBackupContext()
    {
        if (!IsUnlocked || string.IsNullOrWhiteSpace(VaultPath))
            return null;

        return new AutomaticBackupContext(VaultPath, VaultKey, _backupSchedule, _automaticBackupState);
    }

    private void RecordAutomaticBackupResult(AutomaticBackupRunResult? result)
    {
        if (result is null || !result.Success || string.IsNullOrWhiteSpace(result.BackupPath))
            return;

        _backupCenterHistory.LastAutomaticBackupPath = result.BackupPath;
        _backupCenterHistory.AddEntry(new BackupCenterHistoryEntry
        {
            Operation = AutomaticBackupCoordinator.OperationAutomaticBackup,
            Status = result.Status,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            VaultName = GetVaultDisplayName(VaultPath),
            FileName = Path.GetFileName(result.BackupPath),
            FullPath = result.BackupPath,
            ItemCount = result.Summary?.ItemCount ?? 0,
            LabelCount = result.Summary?.LabelCount ?? 0
        });

        LogActivity(
            category: "transfer",
            title: "Automatic backup completed",
            detail: $"Created and verified automatic backup named {Path.GetFileName(result.BackupPath)}.",
            severity: "success",
            vaultPath: VaultPath,
            affectedItem: Path.GetFileName(result.BackupPath));
    }
}
