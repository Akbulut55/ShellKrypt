using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Backups;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed partial class BackupHistoryViewModel : ViewModelBase
{
    public const string EncryptedBackup = "encrypted-backup";
    public const string VerifyBackup = "verify-backup";
    public const string RestoreBackup = "restore-backup";
    public const string PlaintextExport = "plaintext-export";
    public const string CsvImport = "csv-import";

    private readonly BackupCenterContext _context;

    internal BackupHistoryViewModel(BackupCenterContext context)
    {
        _context = context;
        Refresh();
        _context.AutomaticBackupChanged += (_, _) =>
        {
            Refresh();
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    public ObservableCollection<BackupHistoryEntryVm> Entries { get; } = [];
    public bool HasEntries => Entries.Count > 0;
    public event EventHandler? Changed;

    [RelayCommand]
    private void Clear()
    {
        _context.History.RecentEntries.Clear();
        _context.SaveHistory();
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Record(string operation, string status, string path, int itemCount, int labelCount)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? "" : path.Trim();
        switch (operation)
        {
            case EncryptedBackup: _context.History.LastEncryptedBackupPath = normalizedPath; break;
            case VerifyBackup: _context.History.LastVerifiedBackupPath = normalizedPath; break;
            case RestoreBackup: _context.History.LastRestoredBackupPath = normalizedPath; break;
            case PlaintextExport: _context.History.LastPlaintextExportPath = normalizedPath; break;
            case CsvImport: _context.History.LastCsvImportPath = normalizedPath; break;
        }

        _context.History.AddEntry(new BackupCenterHistoryEntry
        {
            Operation = operation,
            Status = status,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            VaultName = _context.VaultDisplayName,
            FileName = Path.GetFileName(normalizedPath),
            FullPath = normalizedPath,
            ItemCount = itemCount,
            LabelCount = labelCount
        });
        _context.SaveHistory();
        Refresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public override void RefreshLocalization()
    {
        foreach (var row in Entries)
            row.RefreshLocalization();
    }

    private void Refresh()
    {
        Entries.Clear();
        foreach (var entry in _context.History.RecentEntries)
            Entries.Add(new BackupHistoryEntryVm(entry, _context.Localization));
        OnPropertyChanged(nameof(HasEntries));
    }
}

public sealed partial class BackupHistoryEntryVm(BackupCenterHistoryEntry entry, LocalizationService localization) : ObservableObject
{
    public string OperationLabel => entry.Operation switch
    {
        "encrypted-backup" => T("BackupCenter.Operation.EncryptedBackup"),
        "verify-backup" => T("BackupCenter.Operation.VerifyBackup"),
        "automatic-backup" => T("BackupCenter.Operation.AutomaticBackup"),
        "restore-backup" => T("BackupCenter.Operation.RestoreBackup"),
        "plaintext-export" => T("BackupCenter.Operation.PlaintextExport"),
        "csv-import" => T("BackupCenter.Operation.CsvImport"),
        _ => entry.Operation
    };
    public string StatusLabel => entry.Status switch
    {
        "success" => T("BackupCenter.StatusChip.Success"),
        "warning" => T("BackupCenter.StatusChip.Warning"),
        "error" => T("BackupCenter.StatusChip.Error"),
        _ => entry.Status
    };
    public string TimestampDisplay => DateTimeOffset.TryParse(entry.TimestampUtc, out var parsed) ? parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm") : entry.TimestampUtc;
    public string FileDisplay => string.IsNullOrWhiteSpace(entry.FileName) ? T("BackupCenter.Status.NoFile") : entry.FileName;
    public string FullPathDisplay => entry.FullPath;
    public string CountsDisplay => entry.ItemCount > 0 || entry.LabelCount > 0
        ? T("BackupCenter.History.Counts", entry.ItemCount, entry.LabelCount)
        : T("BackupCenter.History.NoCounts");
    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(OperationLabel));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(FileDisplay));
        OnPropertyChanged(nameof(CountsDisplay));
    }
    private string T(string key, params object[] args) => localization.Get(key, args);
}
