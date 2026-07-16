using ShellKrypt.Application.Backups;
using ShellKrypt.Desktop.Features.BackupCenter;

namespace ShellKrypt.Desktop.Shell.Runtime;

public sealed class AutomaticBackupController : IAutomaticBackupController
{
    private readonly AutomaticBackupCoordinator _coordinator;
    private readonly IDesktopSettingsController _settings;
    private readonly IVaultSessionController _session;
    private readonly IActivityRecorder _activity;

    public AutomaticBackupController(
        AutomaticBackupCoordinator coordinator,
        IDesktopSettingsController settings,
        IVaultSessionController session,
        IActivityRecorder activity)
    {
        _coordinator = coordinator;
        _settings = settings;
        _session = session;
        _activity = activity;
        _coordinator.StateChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _coordinator.RunCompleted += (_, result) =>
        {
            RecordResult(result);
            SaveSchedule();
        };
    }

    public event EventHandler? Changed;
    public BackupCenterHistory History => _settings.BackupCenterHistory;
    public BackupScheduleSettings Schedule => _settings.BackupSchedule;
    public AutomaticBackupState State => _settings.AutomaticBackupState;
    public bool HasSessionPassphrase => _coordinator.HasSessionPassphrase;
    public bool IsRunning => _coordinator.IsRunning;

    public void Start() => _coordinator.Start();
    public void Stop() => _coordinator.Stop();
    public void SetSessionPassphrase(string? passphrase) => _coordinator.SetSessionPassphrase(passphrase);
    public void ClearSessionPassphrase() => _coordinator.ClearSessionPassphrase();

    public async Task<AutomaticBackupRunResult> RunNowAsync()
    {
        var result = await _coordinator.RunNowAsync();
        RecordResult(result);
        SaveSchedule();
        return result;
    }

    public void SaveHistory() => _settings.SaveBackupCenterHistory();

    public void SaveSchedule()
    {
        _settings.SaveBackupScheduleState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RecordResult(AutomaticBackupRunResult? result)
    {
        if (result is null || !result.Success || string.IsNullOrWhiteSpace(result.BackupPath))
            return;

        History.LastAutomaticBackupPath = result.BackupPath;
        History.AddEntry(new BackupCenterHistoryEntry
        {
            Operation = AutomaticBackupCoordinator.OperationAutomaticBackup,
            Status = result.Status,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            VaultName = GetVaultDisplayName(_session.VaultPath),
            FileName = Path.GetFileName(result.BackupPath),
            FullPath = result.BackupPath,
            ItemCount = result.Summary?.ItemCount ?? 0,
            LabelCount = result.Summary?.LabelCount ?? 0
        });

        _activity.Log(
            "transfer",
            "Automatic backup completed",
            $"Created and verified automatic backup named {Path.GetFileName(result.BackupPath)}.",
            "success",
            _session.VaultPath,
            Path.GetFileName(result.BackupPath));
    }

    private static string GetVaultDisplayName(string? vaultPath)
        => string.IsNullOrWhiteSpace(vaultPath) ? "Vault" : Path.GetFileNameWithoutExtension(vaultPath);
}
