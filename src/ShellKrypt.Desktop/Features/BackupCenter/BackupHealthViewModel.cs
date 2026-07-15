using ShellKrypt.Application.Backups;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed class BackupHealthViewModel : ViewModelBase
{
    private readonly BackupCenterContext _context;

    internal BackupHealthViewModel(BackupCenterContext context, BackupHistoryViewModel history)
    {
        _context = context;
        history.Changed += (_, _) => Refresh();
        _context.AutomaticBackupChanged += (_, _) => Refresh();
    }

    public string ActiveVaultDisplay => _context.VaultDisplayName;
    public string ActiveVaultPathDisplay => string.IsNullOrWhiteSpace(_context.VaultPath)
        ? T("BackupCenter.Status.NoActiveVaultPath")
        : Path.GetFileName(_context.VaultPath);
    public bool BackupExists => LastBackupEntry is not null || !string.IsNullOrWhiteSpace(_context.History.LastEncryptedBackupPath) || !string.IsNullOrWhiteSpace(_context.History.LastAutomaticBackupPath);
    public bool BackupVerified => LastVerifiedEntry is not null || !string.IsNullOrWhiteSpace(_context.History.LastVerifiedBackupPath) || !string.IsNullOrWhiteSpace(_context.AutomaticState.LastVerifiedAtUtc);
    public bool AutomaticConfigured => _context.Schedule.Enabled && !string.IsNullOrWhiteSpace(_context.Schedule.BackupDirectory);
    public string BackupStatus => BackupExists ? T("BackupCenter.Health.Status.Created") : T("BackupCenter.Health.Status.Missing");
    public string VerificationStatus => BackupVerified ? T("BackupCenter.Health.Status.Verified") : T("BackupCenter.Health.Status.NotVerified");
    public string AutomaticStatus => AutomaticConfigured ? T("BackupCenter.Health.Status.Enabled") : T("BackupCenter.Health.Status.Disabled");
    public string BackupDetail => BackupExists ? LastBackupDisplay : T("BackupCenter.Health.Backup.Missing");
    public string VerificationDetail => BackupVerified ? LastVerifiedDisplay : T("BackupCenter.Health.Verification.Missing");
    public string AutomaticDetail => BuildAutomaticDisplay();
    public string BackupBrush => BackupExists ? "SuccessForegroundBrush" : "WarningBrush";
    public string VerificationBrush => BackupVerified ? "SuccessForegroundBrush" : "WarningBrush";
    public string AutomaticBrush => AutomaticConfigured ? "SuccessForegroundBrush" : "WarningBrush";
    public string AutomaticSessionText => _context.HasAutomaticBackupPassphrase
        ? T("BackupCenter.Automatic.Session.Ready")
        : T("BackupCenter.Automatic.Session.Missing");

    private BackupCenterHistoryEntry? LastBackupEntry => _context.History.RecentEntries.FirstOrDefault(entry =>
        (entry.Operation == BackupHistoryViewModel.EncryptedBackup || entry.Operation == AutomaticBackupCoordinator.OperationAutomaticBackup) &&
        entry.Status == AutomaticBackupCoordinator.StatusSuccess);
    private BackupCenterHistoryEntry? LastVerifiedEntry => _context.History.RecentEntries.FirstOrDefault(entry =>
        entry.Operation == BackupHistoryViewModel.VerifyBackup && entry.Status == AutomaticBackupCoordinator.StatusSuccess);
    private string LastBackupDisplay => LastBackupEntry is null
        ? FormatKnown(BackupHistoryViewModel.EncryptedBackup, FirstNotEmpty(_context.History.LastEncryptedBackupPath, _context.History.LastAutomaticBackupPath))
        : T("BackupCenter.Health.Format.LastBackup", LastBackupEntry.FileName, FormatTimestamp(LastBackupEntry.TimestampUtc));
    private string LastVerifiedDisplay
    {
        get
        {
            var timestamp = LastVerifiedEntry?.TimestampUtc ?? _context.AutomaticState.LastVerifiedAtUtc;
            var file = LastVerifiedEntry?.FileName ?? _context.AutomaticState.LastBackupFileName ?? Path.GetFileName(_context.History.LastVerifiedBackupPath) ?? "";
            return string.IsNullOrWhiteSpace(timestamp)
                ? FormatKnown(BackupHistoryViewModel.VerifyBackup, _context.History.LastVerifiedBackupPath)
                : T("BackupCenter.Health.Format.LastVerified", file, FormatTimestamp(timestamp));
        }
    }

    public override void RefreshLocalization() => Refresh();

    public void Refresh() => NotifyLocalized(
        nameof(ActiveVaultDisplay), nameof(ActiveVaultPathDisplay), nameof(BackupExists), nameof(BackupVerified),
        nameof(AutomaticConfigured), nameof(BackupStatus), nameof(VerificationStatus), nameof(AutomaticStatus),
        nameof(BackupDetail), nameof(VerificationDetail), nameof(AutomaticDetail), nameof(BackupBrush),
        nameof(VerificationBrush), nameof(AutomaticBrush), nameof(AutomaticSessionText));

    private string BuildAutomaticDisplay()
    {
        if (!_context.Schedule.Enabled)
            return T("BackupCenter.Health.Automatic.Disabled");
        if (string.IsNullOrWhiteSpace(_context.AutomaticState.LastSuccessfulAtUtc))
            return T("BackupCenter.Health.Automatic.EnabledNoRun");
        return T("BackupCenter.Health.Format.LastAutomatic", _context.AutomaticState.LastBackupFileName, FormatTimestamp(_context.AutomaticState.LastSuccessfulAtUtc));
    }

    private string FormatKnown(string operation, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return T("BackupCenter.Health.Status.NoBackup");
        var entry = _context.History.RecentEntries.FirstOrDefault(x => x.Operation == operation && string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase));
        return entry is null ? Path.GetFileName(path) : T("BackupCenter.Format.LastOperation", entry.FileName, FormatTimestamp(entry.TimestampUtc));
    }

    private string T(string key, params object[] args) => _context.T(key, args);
    private static string FormatTimestamp(string? value) => DateTimeOffset.TryParse(value, out var parsed) ? parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "";
    private static string FirstNotEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}
