using ShellKrypt.Application.Backups;
using ShellKrypt.Desktop.Features.BackupCenter;

namespace ShellKrypt.Desktop.Services.Runtime;

public interface IAutomaticBackupController
{
    event EventHandler? Changed;
    BackupCenterHistory History { get; }
    BackupScheduleSettings Schedule { get; }
    AutomaticBackupState State { get; }
    bool HasSessionPassphrase { get; }
    bool IsRunning { get; }
    void Start();
    void Stop();
    void SetSessionPassphrase(string? passphrase);
    void ClearSessionPassphrase();
    Task<AutomaticBackupRunResult> RunNowAsync();
    void SaveHistory();
    void SaveSchedule();
}
