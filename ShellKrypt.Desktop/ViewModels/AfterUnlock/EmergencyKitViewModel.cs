using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Settings;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class EmergencyKitViewModel : ViewModelBase
{
    private const string OperationEmergencyKitExport = "emergency-kit-export";

    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private bool _isLoading;

    [ObservableProperty] private bool noPasswordRecoveryAcknowledged;
    [ObservableProperty] private bool masterPasswordStoredExternally;
    [ObservableProperty] private bool backupPassphraseStoredExternally;
    [ObservableProperty] private bool backupLocationKnown;
    [ObservableProperty] private bool backupVerified;
    [ObservableProperty] private string status = "";

    public EmergencyKitViewModel(MainWindowViewModel root, ShellViewModel shell)
    {
        _root = root;
        _shell = shell;
        _root.AutomaticBackupChanged += (_, _) => RefreshReadiness();
        LoadChecklistState();
        RefreshReadiness();
    }

    public ObservableCollection<EmergencyReadinessItemVm> ReadinessItems { get; } = new();

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public int ChecklistCompletedCount => ChecklistValues.Count(value => value);
    public int ChecklistTotalCount => ChecklistValues.Length;
    public string ChecklistProgressText => T("EmergencyKit.Checklist.Progress", ChecklistCompletedCount, ChecklistTotalCount);
    public string ReadinessScoreDisplay => $"{ReadinessScore}%";
    public string ReadinessScoreTitle => ReadinessScore switch
    {
        >= 90 => T("EmergencyKit.Score.Ready"),
        >= 60 => T("EmergencyKit.Score.Partial"),
        _ => T("EmergencyKit.Score.NeedsWork")
    };
    public string LastChecklistExportDisplay => string.IsNullOrWhiteSpace(_root.EmergencyKit.LastChecklistExportPath)
        ? T("EmergencyKit.Status.NeverExported")
        : Path.GetFileName(_root.EmergencyKit.LastChecklistExportPath);
    public string LastBackupDisplay => LastBackupEntry is null
        ? T("EmergencyKit.Status.NoBackup")
        : T("EmergencyKit.Format.LastBackup", LastBackupEntry.FileName, FormatTimestamp(LastBackupEntry.TimestampUtc));
    public string LastVerifiedDisplay => LastVerifiedBackupEntry is null && string.IsNullOrWhiteSpace(_root.AutomaticBackupState.LastVerifiedAtUtc)
        ? T("EmergencyKit.Status.NoVerifiedBackup")
        : T("EmergencyKit.Format.LastVerified", LastVerifiedFileName, FormatTimestamp(LastVerifiedTimestampUtc));
    public string AutomaticBackupDisplay => BuildAutomaticBackupDisplay();

    private bool[] ChecklistValues =>
    [
        NoPasswordRecoveryAcknowledged,
        MasterPasswordStoredExternally,
        BackupPassphraseStoredExternally,
        BackupLocationKnown,
        BackupVerified
    ];

    private int ReadinessScore
    {
        get
        {
            var ready = ReadinessItems.Count(item => item.IsReady);
            return ReadinessItems.Count == 0 ? 0 : (int)Math.Round(ready * 100d / ReadinessItems.Count);
        }
    }

    private BackupCenterHistoryEntry? LastBackupEntry => _root.BackupCenterHistory.RecentEntries
        .FirstOrDefault(entry =>
            (entry.Operation == "encrypted-backup" || entry.Operation == AutomaticBackupCoordinator.OperationAutomaticBackup) &&
            entry.Status == AutomaticBackupCoordinator.StatusSuccess);

    private BackupCenterHistoryEntry? LastVerifiedBackupEntry => _root.BackupCenterHistory.RecentEntries
        .FirstOrDefault(entry => entry.Operation == "verify-backup" && entry.Status == AutomaticBackupCoordinator.StatusSuccess);

    private string LastVerifiedTimestampUtc => LastVerifiedBackupEntry?.TimestampUtc
        ?? _root.AutomaticBackupState.LastVerifiedAtUtc;

    private string LastVerifiedFileName => LastVerifiedBackupEntry?.FileName
        ?? _root.AutomaticBackupState.LastBackupFileName
        ?? "";

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
    partial void OnNoPasswordRecoveryAcknowledgedChanged(bool value) => SaveChecklistState();
    partial void OnMasterPasswordStoredExternallyChanged(bool value) => SaveChecklistState();
    partial void OnBackupPassphraseStoredExternallyChanged(bool value) => SaveChecklistState();
    partial void OnBackupLocationKnownChanged(bool value) => SaveChecklistState();
    partial void OnBackupVerifiedChanged(bool value) => SaveChecklistState();

    public override void RefreshLocalization()
    {
        RefreshReadiness();
        NotifyLocalized(
            nameof(ChecklistProgressText),
            nameof(ReadinessScoreDisplay),
            nameof(ReadinessScoreTitle),
            nameof(LastChecklistExportDisplay),
            nameof(LastBackupDisplay),
            nameof(LastVerifiedDisplay),
            nameof(AutomaticBackupDisplay));
    }

    [RelayCommand]
    private void OpenBackupCenter() => _shell.ShowBackupCenter();

    [RelayCommand]
    private void VerifyBackup() => _shell.ShowBackupCenter();

    [RelayCommand]
    private void CreateBackup() => _shell.ShowBackupCenter();

    [RelayCommand]
    private async Task ExportChecklistAsync()
    {
        var suggestedName = $"ShellKrypt-{SafeVaultName()}-Emergency-Kit.txt";
        var path = await _root.PickSaveFileAsync(
            T("EmergencyKit.Picker.ExportTitle"),
            suggestedName,
            ".txt",
            [".txt"],
            T("EmergencyKit.Picker.TextFile"));

        if (string.IsNullOrWhiteSpace(path))
            return;

        var text = BuildPrintableChecklistText();
        await File.WriteAllTextAsync(path, text, Encoding.UTF8);

        _root.EmergencyKit.LastChecklistExportPath = path;
        _root.EmergencyKit.LastUpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        _root.BackupCenterHistory.LastEmergencyKitExportPath = path;
        _root.BackupCenterHistory.AddEntry(new BackupCenterHistoryEntry
        {
            Operation = OperationEmergencyKitExport,
            Status = AutomaticBackupCoordinator.StatusSuccess,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            VaultName = SafeVaultName(),
            FileName = Path.GetFileName(path),
            FullPath = path
        });

        _root.SaveEmergencyKitState();
        _root.SaveBackupCenterHistory();
        _root.LogActivity(
            "transfer",
            "Emergency kit exported",
            $"Saved emergency checklist named {Path.GetFileName(path)}.",
            "success",
            _root.VaultPath,
            Path.GetFileName(path));

        Status = T("EmergencyKit.Status.Exported", Path.GetFileName(path));
        RefreshReadiness();
    }

    private void LoadChecklistState()
    {
        _isLoading = true;
        NoPasswordRecoveryAcknowledged = _root.EmergencyKit.NoPasswordRecoveryAcknowledged;
        MasterPasswordStoredExternally = _root.EmergencyKit.MasterPasswordStoredExternally;
        BackupPassphraseStoredExternally = _root.EmergencyKit.BackupPassphraseStoredExternally;
        BackupLocationKnown = _root.EmergencyKit.BackupLocationKnown;
        BackupVerified = _root.EmergencyKit.BackupVerified;
        _isLoading = false;
    }

    private void SaveChecklistState()
    {
        if (_isLoading)
            return;

        _root.EmergencyKit.NoPasswordRecoveryAcknowledged = NoPasswordRecoveryAcknowledged;
        _root.EmergencyKit.MasterPasswordStoredExternally = MasterPasswordStoredExternally;
        _root.EmergencyKit.BackupPassphraseStoredExternally = BackupPassphraseStoredExternally;
        _root.EmergencyKit.BackupLocationKnown = BackupLocationKnown;
        _root.EmergencyKit.BackupVerified = BackupVerified;
        _root.EmergencyKit.LastUpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        _root.SaveEmergencyKitState();
        RefreshReadiness();
    }

    private void RefreshReadiness()
    {
        ReadinessItems.Clear();
        AddReadiness(
            NoPasswordRecoveryAcknowledged || _root.HasAcceptedSecurityAcknowledgement,
            "EmergencyKit.Readiness.NoRecovery.Title",
            "EmergencyKit.Readiness.NoRecovery.Detail");
        AddReadiness(
            LastBackupEntry is not null || !string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastEncryptedBackupPath) || !string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastAutomaticBackupPath),
            "EmergencyKit.Readiness.BackupExists.Title",
            "EmergencyKit.Readiness.BackupExists.Detail");
        AddReadiness(
            BackupVerified || LastVerifiedBackupEntry is not null || !string.IsNullOrWhiteSpace(_root.AutomaticBackupState.LastVerifiedAtUtc),
            "EmergencyKit.Readiness.BackupVerified.Title",
            "EmergencyKit.Readiness.BackupVerified.Detail");
        AddReadiness(
            _root.BackupSchedule.Enabled && !string.IsNullOrWhiteSpace(_root.BackupSchedule.BackupDirectory),
            "EmergencyKit.Readiness.AutomaticBackup.Title",
            "EmergencyKit.Readiness.AutomaticBackup.Detail");
        AddReadiness(
            _root.AutoLockEnabled && _root.LockOnDeactivate && _root.ClipboardClearSeconds > 0,
            "EmergencyKit.Readiness.SessionSecurity.Title",
            "EmergencyKit.Readiness.SessionSecurity.Detail");

        NotifyLocalized(
            nameof(ChecklistCompletedCount),
            nameof(ChecklistProgressText),
            nameof(ReadinessScoreDisplay),
            nameof(ReadinessScoreTitle),
            nameof(LastChecklistExportDisplay),
            nameof(LastBackupDisplay),
            nameof(LastVerifiedDisplay),
            nameof(AutomaticBackupDisplay));
    }

    private void AddReadiness(bool isReady, string titleKey, string detailKey)
        => ReadinessItems.Add(new EmergencyReadinessItemVm(isReady, T(titleKey), T(detailKey)));

    public string BuildPrintableChecklistText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("ShellKrypt Emergency Kit");
        builder.AppendLine("========================");
        builder.AppendLine();
        builder.AppendLine($"Vault: {SafeVaultName()}");
        builder.AppendLine($"Exported: {DateTimeOffset.Now:yyyy-MM-dd HH:mm}");
        builder.AppendLine();
        builder.AppendLine("Recovery facts");
        builder.AppendLine("- ShellKrypt has no password recovery.");
        builder.AppendLine("- Keep your master password and backup passphrase outside ShellKrypt.");
        builder.AppendLine("- Encrypted .skbx backups require their backup passphrase.");
        builder.AppendLine();
        builder.AppendLine("Status");
        builder.AppendLine($"- Last backup: {LastBackupDisplay}");
        builder.AppendLine($"- Last verified backup: {LastVerifiedDisplay}");
        builder.AppendLine($"- Automatic backups: {AutomaticBackupDisplay}");
        builder.AppendLine();
        builder.AppendLine("Checklist");
        builder.AppendLine($"- No password recovery understood: {FormatChecked(NoPasswordRecoveryAcknowledged)}");
        builder.AppendLine($"- Master password stored outside ShellKrypt: {FormatChecked(MasterPasswordStoredExternally)}");
        builder.AppendLine($"- Backup passphrase stored outside ShellKrypt: {FormatChecked(BackupPassphraseStoredExternally)}");
        builder.AppendLine($"- Backup location known: {FormatChecked(BackupLocationKnown)}");
        builder.AppendLine($"- Backup verified: {FormatChecked(BackupVerified)}");
        builder.AppendLine();
        builder.AppendLine("This file intentionally does not contain passwords, backup passphrases, vault keys, card numbers, API secrets, OTP seeds, or note contents.");
        return builder.ToString();
    }

    private string BuildAutomaticBackupDisplay()
    {
        if (!_root.BackupSchedule.Enabled)
            return T("EmergencyKit.Status.AutomaticDisabled");

        if (string.IsNullOrWhiteSpace(_root.AutomaticBackupState.LastSuccessfulAtUtc))
            return T("EmergencyKit.Status.AutomaticEnabledNoRun");

        return T("EmergencyKit.Format.LastAutomatic", _root.AutomaticBackupState.LastBackupFileName, FormatTimestamp(_root.AutomaticBackupState.LastSuccessfulAtUtc));
    }

    private string SafeVaultName()
        => string.IsNullOrWhiteSpace(_root.VaultPath)
            ? T("Common.NoVaultSelected")
            : Path.GetFileNameWithoutExtension(_root.VaultPath);

    private string T(string key, params object[] args) => _root.Localization.Get(key, args);

    private static string FormatTimestamp(string? timestampUtc)
        => DateTimeOffset.TryParse(timestampUtc, out var parsed) ? parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "";

    private static string FormatChecked(bool value) => value ? "YES" : "NO";
}

public sealed class EmergencyReadinessItemVm
{
    public EmergencyReadinessItemVm(bool isReady, string title, string detail)
    {
        IsReady = isReady;
        Title = title;
        Detail = detail;
    }

    public bool IsReady { get; }
    public string Title { get; }
    public string Detail { get; }
    public string StatusText => IsReady ? "OK" : "--";
    public string StatusBrushKey => IsReady ? "AccentBrush" : "TextMutedBrush";
}
