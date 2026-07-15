using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using ShellKrypt.Application.Backups;
using ShellKrypt.Core.Backups;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed class AutomaticBackupCoordinator
{
    public const string OperationAutomaticBackup = "automatic-backup";
    public const string StatusSuccess = "success";
    public const string StatusWarning = "warning";
    public const string StatusError = "error";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private readonly DispatcherTimer _timer = new();
    private readonly IEncryptedVaultBackupService _backupService;
    private readonly IAutomaticBackupFileStore _backupFiles;
    private readonly Func<AutomaticBackupContext?> _contextProvider;
    private string _sessionPassphrase = "";
    private bool _isRunning;

    public AutomaticBackupCoordinator(
        IEncryptedVaultBackupService backupService,
        IAutomaticBackupFileStore backupFiles,
        Func<AutomaticBackupContext?> contextProvider)
    {
        _backupService = backupService;
        _backupFiles = backupFiles;
        _contextProvider = contextProvider;
        _timer.Interval = CheckInterval;
        _timer.Tick += (_, _) => _ = CheckDueAsync();
    }

    public event EventHandler? StateChanged;
    public event EventHandler<AutomaticBackupRunResult>? RunCompleted;

    public bool IsRunning => _isRunning;
    public bool HasSessionPassphrase => !string.IsNullOrWhiteSpace(_sessionPassphrase);

    public void Start()
    {
        if (!_timer.IsEnabled)
            _timer.Start();

        _ = CheckDueAsync();
    }

    public void Stop() => _timer.Stop();

    public void SetSessionPassphrase(string? passphrase)
    {
        _sessionPassphrase = passphrase?.Trim() ?? "";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSessionPassphrase()
    {
        if (string.IsNullOrEmpty(_sessionPassphrase))
            return;

        _sessionPassphrase = "";
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<AutomaticBackupRunResult?> CheckDueAsync()
    {
        var context = _contextProvider();
        if (context is null || !ShouldRun(context.Schedule, context.State, DateTimeOffset.UtcNow))
            return null;

        if (_isRunning || string.IsNullOrWhiteSpace(_sessionPassphrase))
            return null;

        var result = await RunAsync(context, manual: false);
        RunCompleted?.Invoke(this, result);
        return result;
    }

    public async Task<AutomaticBackupRunResult> RunNowAsync()
    {
        var context = _contextProvider();
        return context is null
            ? AutomaticBackupRunResult.Warning("Unlock a vault before running an automatic backup.")
            : await RunAsync(context, manual: true);
    }

    public static bool ShouldRun(BackupScheduleSettings schedule, AutomaticBackupState state, DateTimeOffset nowUtc)
    {
        schedule.Normalize();
        state.Normalize();

        if (!schedule.Enabled || string.IsNullOrWhiteSpace(schedule.BackupDirectory))
            return false;

        if (!DateTimeOffset.TryParse(state.LastSuccessfulAtUtc, out var lastSuccess))
            return true;

        return nowUtc - lastSuccess.ToUniversalTime() >= schedule.Interval;
    }

    private async Task<AutomaticBackupRunResult> RunAsync(AutomaticBackupContext context, bool manual)
    {
        if (_isRunning)
            return AutomaticBackupRunResult.Warning("Automatic backup is already running.");

        context.Schedule.Normalize();
        context.State.Normalize();

        if (!context.Schedule.Enabled && !manual)
            return AutomaticBackupRunResult.Warning("Automatic backups are disabled.");

        if (string.IsNullOrWhiteSpace(context.Schedule.BackupDirectory))
            return FinishWarning(context.State, "Choose an automatic backup directory first.");

        if (string.IsNullOrWhiteSpace(_sessionPassphrase))
            return FinishWarning(context.State, "Enter the automatic backup passphrase for this unlocked session.");

        _isRunning = true;
        var attemptedAt = DateTimeOffset.UtcNow;
        context.State.LastAttemptedAtUtc = attemptedAt.ToString("O");
        context.State.LastStatus = "running";
        context.State.LastError = "";
        StateChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            var directory = Path.GetFullPath(context.Schedule.BackupDirectory);
            Directory.CreateDirectory(directory);

            var backupPath = _backupFiles.BuildBackupPath(directory, context.VaultPath, attemptedAt);
            var summary = await _backupService.GetSummaryAsync(context.VaultPath, context.VaultKey);
            await _backupService.CreateAsync(context.VaultPath, context.VaultKey, backupPath, _sessionPassphrase);
            var verified = await _backupService.InspectAsync(backupPath, _sessionPassphrase);

            if (verified.ItemCount != summary.ItemCount || verified.LabelCount != summary.LabelCount)
                throw new InvalidOperationException("Automatic backup verification returned a different item or label count.");

            _backupFiles.ApplyRetention(directory, context.VaultPath, context.Schedule.RetentionCount);

            var completedAt = DateTimeOffset.UtcNow;
            context.State.LastSuccessfulAtUtc = completedAt.ToString("O");
            context.State.LastVerifiedAtUtc = completedAt.ToString("O");
            context.State.LastBackupPath = backupPath;
            context.State.LastBackupFileName = Path.GetFileName(backupPath);
            context.State.LastStatus = StatusSuccess;
            context.State.LastError = "";
            return new AutomaticBackupRunResult(true, StatusSuccess, "Automatic backup completed and verified.", backupPath, summary);
        }
        catch (Exception ex)
        {
            context.State.LastStatus = StatusError;
            context.State.LastError = ex.Message;
            return new AutomaticBackupRunResult(false, StatusError, ex.Message, null, null);
        }
        finally
        {
            _isRunning = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private AutomaticBackupRunResult FinishWarning(AutomaticBackupState state, string message)
    {
        state.LastAttemptedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        state.LastStatus = StatusWarning;
        state.LastError = message;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return AutomaticBackupRunResult.Warning(message);
    }

}

public sealed record AutomaticBackupContext(
    string VaultPath,
    byte[] VaultKey,
    BackupScheduleSettings Schedule,
    AutomaticBackupState State);

public sealed record AutomaticBackupRunResult(
    bool Success,
    string Status,
    string Message,
    string? BackupPath,
    VaultSnapshotSummary? Summary)
{
    public static AutomaticBackupRunResult Warning(string message)
        => new(false, AutomaticBackupCoordinator.StatusWarning, message, null, null);
}
