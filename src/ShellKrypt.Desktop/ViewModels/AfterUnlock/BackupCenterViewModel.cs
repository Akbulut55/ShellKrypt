using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Settings;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Services;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class BackupCenterViewModel : ViewModelBase
{
    private const string OperationEncryptedBackup = "encrypted-backup";
    private const string OperationVerifyBackup = "verify-backup";
    private const string OperationRestoreBackup = "restore-backup";
    private const string OperationPlaintextExport = "plaintext-export";
    private const string OperationCsvImport = "csv-import";
    private const string BackupModeCreate = "create";
    private const string BackupModeVerify = "verify";
    private const string BackupModeRestore = "restore";

    private readonly MainWindowViewModel _root;
    private readonly IVaultTransferService _transferService;

    [ObservableProperty] private string encryptedExportPath = "";
    [ObservableProperty] private string exportPassphrase = "";
    [ObservableProperty] private string exportSummary = "";
    [ObservableProperty] private string verifyBackupPath = "";
    [ObservableProperty] private string verifyPassphrase = "";
    [ObservableProperty] private string verifySummary = "";
    [ObservableProperty] private string restoreBackupPath = "";
    [ObservableProperty] private string restorePassphrase = "";
    [ObservableProperty] private string restoreSummary = "";
    [ObservableProperty] private bool confirmRestore;
    [ObservableProperty] private string plaintextExportPath = "";
    [ObservableProperty] private bool confirmPlaintextExport;
    [ObservableProperty] private string plaintextExportConfirmationText = "";
    [ObservableProperty] private string csvImportPath = "";
    [ObservableProperty] private string csvPreviewSummary = "";
    [ObservableProperty] private bool isTransferBusy;
    [ObservableProperty] private string transferStatus = "";
    [ObservableProperty] private CsvDuplicateStrategyOption? selectedCsvDuplicateStrategyOption;
    [ObservableProperty] private bool automaticBackupEnabled;
    [ObservableProperty] private string automaticBackupDirectory = "";
    [ObservableProperty] private string automaticBackupPassphrase = "";
    [ObservableProperty] private int automaticBackupRetentionCount = BackupScheduleSettings.DefaultRetentionCount;
    [ObservableProperty] private AutomaticBackupFrequencyOption? selectedAutomaticBackupFrequencyOption;
    [ObservableProperty] private string automaticBackupStatus = "";
    [ObservableProperty] private string encryptedBackupMode = BackupModeCreate;

    public BackupCenterViewModel(MainWindowViewModel root)
    {
        _root = root;
        _transferService = _root.VaultTransferService;
        foreach (var option in CreateDuplicateStrategyOptions())
            CsvDuplicateStrategyOptions.Add(option);
        foreach (var option in CreateAutomaticBackupFrequencyOptions())
            AutomaticBackupFrequencyOptions.Add(option);

        RefreshOptionLabels();
        SelectedCsvDuplicateStrategyOption = CsvDuplicateStrategyOptions[0];
        LoadAutomaticBackupSettings();
        _root.AutomaticBackupChanged += (_, _) =>
        {
            RefreshAutomaticBackupState();
        };

        var exportBaseName = GetVaultDisplayName();
        var history = _root.BackupCenterHistory;
        EncryptedExportPath = UseHistoryOrDefault(history.LastEncryptedBackupPath, DefaultPaths.GetSuggestedExportPath($"{exportBaseName} Backup", ".skbx"));
        PlaintextExportPath = UseHistoryOrDefault(history.LastPlaintextExportPath, DefaultPaths.GetSuggestedExportPath($"{exportBaseName} DECRYPTED Plaintext Export", ".json"));
        VerifyBackupPath = FirstNotEmpty(history.LastVerifiedBackupPath, history.LastEncryptedBackupPath);
        RestoreBackupPath = FirstNotEmpty(history.LastRestoredBackupPath, history.LastVerifiedBackupPath, history.LastEncryptedBackupPath);
        CsvImportPath = history.LastCsvImportPath;
        RefreshHistoryRows();
        RefreshBackupHealth();
    }

    public ObservableCollection<CsvDuplicateStrategyOption> CsvDuplicateStrategyOptions { get; } = new();
    public ObservableCollection<AutomaticBackupFrequencyOption> AutomaticBackupFrequencyOptions { get; } = new();
    public ObservableCollection<VaultCsvImportRowPreview> CsvPreviewRows { get; } = new();
    public ObservableCollection<BackupHistoryEntryVm> RecentHistory { get; } = new();

    public VaultCsvDuplicateStrategy SelectedCsvDuplicateStrategy
    {
        get => SelectedCsvDuplicateStrategyOption?.Strategy ?? VaultCsvDuplicateStrategy.SkipDuplicates;
        set
        {
            SelectedCsvDuplicateStrategyOption = CsvDuplicateStrategyOptions.FirstOrDefault(option => option.Strategy == value)
                ?? CsvDuplicateStrategyOptions[0];
            OnPropertyChanged();
        }
    }

    public bool HasCsvPreview => CsvPreviewRows.Count > 0;
    public bool HasTransferStatus => !string.IsNullOrWhiteSpace(TransferStatus);
    public bool HasRecentHistory => RecentHistory.Count > 0;
    public bool HasAutomaticBackupStatus => !string.IsNullOrWhiteSpace(AutomaticBackupStatus);
    public bool HasAutomaticBackupSessionPassphrase => _root.AutomaticBackups.HasSessionPassphrase;
    public bool IsAutomaticBackupRunning => _root.AutomaticBackups.IsRunning;
    public string ActiveVaultDisplay => GetVaultDisplayName();
    public string ActiveVaultPathDisplay => string.IsNullOrWhiteSpace(_root.VaultPath) ? T("BackupCenter.Status.NoActiveVaultPath") : Path.GetFileName(_root.VaultPath);
    public string LastEncryptedBackupDisplay => FormatLastOperation(OperationEncryptedBackup, _root.BackupCenterHistory.LastEncryptedBackupPath);
    public string LastVerifiedBackupDisplay => FormatLastOperation(OperationVerifyBackup, _root.BackupCenterHistory.LastVerifiedBackupPath);
    public string LastAutomaticBackupDisplay => FormatLastAutomaticBackup();
    public string LastRestoreDisplay => FormatLastOperation(OperationRestoreBackup, _root.BackupCenterHistory.LastRestoredBackupPath);
    public string LastPlaintextExportDisplay => FormatLastOperation(OperationPlaintextExport, _root.BackupCenterHistory.LastPlaintextExportPath);
    public string LastCsvImportDisplay => FormatLastOperation(OperationCsvImport, _root.BackupCenterHistory.LastCsvImportPath);
    public string AutomaticBackupSessionText => HasAutomaticBackupSessionPassphrase
        ? T("BackupCenter.Automatic.Session.Ready")
        : T("BackupCenter.Automatic.Session.Missing");
    public bool RecoveryBackupExists => LastBackupEntry is not null || !string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastEncryptedBackupPath) || !string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastAutomaticBackupPath);
    public bool RecoveryBackupVerified => LastVerifiedBackupEntry is not null || !string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastVerifiedBackupPath) || !string.IsNullOrWhiteSpace(_root.AutomaticBackupState.LastVerifiedAtUtc);
    public bool RecoveryAutomaticBackupConfigured => _root.BackupSchedule.Enabled && !string.IsNullOrWhiteSpace(_root.BackupSchedule.BackupDirectory);
    public bool IsCreateBackupMode => EncryptedBackupMode == BackupModeCreate;
    public bool IsVerifyBackupMode => EncryptedBackupMode == BackupModeVerify;
    public bool IsRestoreBackupMode => EncryptedBackupMode == BackupModeRestore;
    public string CreateBackupModeBackground => IsCreateBackupMode ? "AccentMutedBrush" : "SurfaceRaisedBrush";
    public string VerifyBackupModeBackground => IsVerifyBackupMode ? "AccentMutedBrush" : "SurfaceRaisedBrush";
    public string RestoreBackupModeBackground => IsRestoreBackupMode ? "AccentMutedBrush" : "SurfaceRaisedBrush";
    public string CreateBackupModeForeground => IsCreateBackupMode ? "AccentBrush" : "TextMutedBrush";
    public string VerifyBackupModeForeground => IsVerifyBackupMode ? "AccentBrush" : "TextMutedBrush";
    public string RestoreBackupModeForeground => IsRestoreBackupMode ? "AccentBrush" : "TextMutedBrush";
    public string BackupHealthBackupStatus => RecoveryBackupExists ? T("BackupCenter.Health.Status.Created") : T("BackupCenter.Health.Status.Missing");
    public string BackupHealthVerificationStatus => RecoveryBackupVerified ? T("BackupCenter.Health.Status.Verified") : T("BackupCenter.Health.Status.NotVerified");
    public string BackupHealthAutomaticStatus => RecoveryAutomaticBackupConfigured ? T("BackupCenter.Health.Status.Enabled") : T("BackupCenter.Health.Status.Disabled");
    public string BackupHealthBackupDetail => RecoveryBackupExists ? RecoveryLastBackupDisplay : T("BackupCenter.Health.Backup.Missing");
    public string BackupHealthVerificationDetail => RecoveryBackupVerified ? RecoveryLastVerifiedDisplay : T("BackupCenter.Health.Verification.Missing");
    public string BackupHealthAutomaticDetail => RecoveryAutomaticBackupDisplay;
    public string BackupHealthBackupBrush => RecoveryBackupExists ? "SuccessForegroundBrush" : "WarningBrush";
    public string BackupHealthVerificationBrush => RecoveryBackupVerified ? "SuccessForegroundBrush" : "WarningBrush";
    public string BackupHealthAutomaticBrush => RecoveryAutomaticBackupConfigured ? "SuccessForegroundBrush" : "WarningBrush";
    public string RecoveryLastBackupDisplay => LastBackupEntry is null
        ? FormatLastKnownBackup()
        : T("BackupCenter.Health.Format.LastBackup", LastBackupEntry.FileName, FormatTimestamp(LastBackupEntry.TimestampUtc));
    public string RecoveryLastVerifiedDisplay => LastVerifiedBackupEntry is null && string.IsNullOrWhiteSpace(_root.AutomaticBackupState.LastVerifiedAtUtc)
        ? FormatLastKnownVerifiedBackup()
        : T("BackupCenter.Health.Format.LastVerified", LastVerifiedFileName, FormatTimestamp(LastVerifiedTimestampUtc));
    public string RecoveryAutomaticBackupDisplay => BuildRecoveryAutomaticBackupDisplay();
    private BackupCenterHistoryEntry? LastBackupEntry => _root.BackupCenterHistory.RecentEntries
        .FirstOrDefault(entry =>
            (entry.Operation == OperationEncryptedBackup || entry.Operation == AutomaticBackupCoordinator.OperationAutomaticBackup) &&
            entry.Status == AutomaticBackupCoordinator.StatusSuccess);
    private BackupCenterHistoryEntry? LastVerifiedBackupEntry => _root.BackupCenterHistory.RecentEntries
        .FirstOrDefault(entry => entry.Operation == OperationVerifyBackup && entry.Status == AutomaticBackupCoordinator.StatusSuccess);
    private string LastVerifiedTimestampUtc => LastVerifiedBackupEntry?.TimestampUtc
        ?? _root.AutomaticBackupState.LastVerifiedAtUtc;
    private string LastVerifiedFileName => LastVerifiedBackupEntry?.FileName
        ?? _root.AutomaticBackupState.LastBackupFileName
        ?? Path.GetFileName(_root.BackupCenterHistory.LastVerifiedBackupPath)
        ?? "";

    partial void OnTransferStatusChanged(string value) => OnPropertyChanged(nameof(HasTransferStatus));
    partial void OnAutomaticBackupStatusChanged(string value) => OnPropertyChanged(nameof(HasAutomaticBackupStatus));
    partial void OnSelectedCsvDuplicateStrategyOptionChanged(CsvDuplicateStrategyOption? value) => OnPropertyChanged(nameof(SelectedCsvDuplicateStrategy));
    partial void OnEncryptedBackupModeChanged(string value) => RefreshEncryptedBackupModeState();
    partial void OnAutomaticBackupPassphraseChanged(string value)
    {
        _root.SetAutomaticBackupSessionPassphrase(value);
        RefreshAutomaticBackupState();
    }

    public override void RefreshLocalization()
    {
        RefreshOptionLabels();

        foreach (var row in RecentHistory)
            row.RefreshLocalization();

        NotifyLocalized(
            nameof(ActiveVaultDisplay),
            nameof(ActiveVaultPathDisplay),
            nameof(LastEncryptedBackupDisplay),
            nameof(LastVerifiedBackupDisplay),
            nameof(LastAutomaticBackupDisplay),
            nameof(LastRestoreDisplay),
            nameof(LastPlaintextExportDisplay),
            nameof(LastCsvImportDisplay),
            nameof(AutomaticBackupSessionText),
            nameof(BackupHealthBackupStatus),
            nameof(BackupHealthVerificationStatus),
            nameof(BackupHealthAutomaticStatus),
            nameof(BackupHealthBackupDetail),
            nameof(BackupHealthVerificationDetail),
            nameof(BackupHealthAutomaticDetail),
            nameof(RecoveryLastBackupDisplay),
            nameof(RecoveryLastVerifiedDisplay),
            nameof(RecoveryAutomaticBackupDisplay));
    }

    private void RefreshOptionLabels()
    {
        foreach (var option in CsvDuplicateStrategyOptions)
            option.RefreshLocalization(_root.Localization);
        foreach (var option in AutomaticBackupFrequencyOptions)
            option.RefreshLocalization(_root.Localization);

        OnPropertyChanged(nameof(SelectedCsvDuplicateStrategyOption));
        OnPropertyChanged(nameof(SelectedAutomaticBackupFrequencyOption));
    }

    [RelayCommand]
    private void ShowCreateBackup() => EncryptedBackupMode = BackupModeCreate;

    [RelayCommand]
    private void ShowVerifyBackup() => EncryptedBackupMode = BackupModeVerify;

    [RelayCommand]
    private void ShowRestoreBackup() => EncryptedBackupMode = BackupModeRestore;

    [RelayCommand]
    private void ConfigureAutomaticBackups()
    {
        AutomaticBackupStatus = T("BackupCenter.Health.Automatic.ConfigureHint");
    }

    [RelayCommand]
    private void ClearRecentHistory()
    {
        _root.BackupCenterHistory.RecentEntries.Clear();
        _root.SaveBackupCenterHistory();
        RefreshHistoryRows();
        RefreshBackupHealth();
        NotifyLocalized(
            nameof(LastEncryptedBackupDisplay),
            nameof(LastVerifiedBackupDisplay),
            nameof(LastRestoreDisplay),
            nameof(LastPlaintextExportDisplay),
            nameof(LastCsvImportDisplay));
    }

    [RelayCommand]
    private async Task BrowseEncryptedExportPathAsync()
    {
        var path = await _root.PickSaveFileAsync(
            T("BackupCenter.Picker.EncryptedBackup.SaveTitle"),
            Path.GetFileNameWithoutExtension(EncryptedExportPath),
            ".skbx",
            [".skbx"],
            T("BackupCenter.Picker.ShellKryptBackup"));

        if (!string.IsNullOrWhiteSpace(path))
            EncryptedExportPath = path;
    }

    [RelayCommand]
    private async Task BrowseVerifyBackupPathAsync()
    {
        var path = await PickBackupAsync(T("BackupCenter.Picker.VerifyBackup.Title"));
        if (!string.IsNullOrWhiteSpace(path))
            VerifyBackupPath = path;
    }

    [RelayCommand]
    private async Task BrowseRestoreBackupPathAsync()
    {
        var path = await PickBackupAsync(T("BackupCenter.Picker.RestoreBackup.Title"));
        if (!string.IsNullOrWhiteSpace(path))
            RestoreBackupPath = path;
    }

    [RelayCommand]
    private async Task BrowsePlaintextExportPathAsync()
    {
        var path = await _root.PickSaveFileAsync(
            T("BackupCenter.Picker.Plaintext.SaveTitle"),
            Path.GetFileNameWithoutExtension(PlaintextExportPath),
            ".json",
            [".json"],
            T("BackupCenter.Picker.JsonExport"));

        if (!string.IsNullOrWhiteSpace(path))
            PlaintextExportPath = path;
    }

    [RelayCommand]
    private async Task BrowseCsvImportPathAsync()
    {
        var path = await _root.PickOpenFileAsync(
            T("BackupCenter.Picker.Csv.Title"),
            [".csv"],
            T("BackupCenter.Picker.Csv.FileType"));

        if (!string.IsNullOrWhiteSpace(path))
            CsvImportPath = path;
    }

    [RelayCommand]
    private async Task BrowseAutomaticBackupDirectoryAsync()
    {
        var path = await _root.PickFolderAsync(T("BackupCenter.Automatic.Picker.DirectoryTitle"));
        if (!string.IsNullOrWhiteSpace(path))
            AutomaticBackupDirectory = path;
    }

    [RelayCommand]
    private void SaveAutomaticBackupSettings()
    {
        _root.BackupSchedule.Enabled = AutomaticBackupEnabled;
        _root.BackupSchedule.BackupDirectory = AutomaticBackupDirectory;
        _root.BackupSchedule.Frequency = SelectedAutomaticBackupFrequencyOption?.Frequency ?? BackupScheduleFrequency.Daily;
        _root.BackupSchedule.RetentionCount = AutomaticBackupRetentionCount;
        _root.SaveBackupScheduleState();
        LoadAutomaticBackupSettings();
        AutomaticBackupStatus = T("BackupCenter.Automatic.Status.Saved");
    }

    [RelayCommand]
    private async Task RunAutomaticBackupNowAsync()
    {
        SaveAutomaticBackupSettings();
        await RunTransferAsync(async () =>
        {
            var result = await _root.RunAutomaticBackupNowAsync();
            AutomaticBackupStatus = result.Success
                ? T("BackupCenter.Automatic.Status.RunSuccess", Path.GetFileName(result.BackupPath ?? ""))
                : result.Message;
            TransferStatus = AutomaticBackupStatus;
            RefreshAutomaticBackupState();
        });
    }

    [RelayCommand]
    private async Task PreviewExportAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        await RunTransferAsync(async () =>
        {
            var summary = await _transferService.GetExportSummaryAsync(vaultPath, vaultKey);
            ExportSummary = FormatExportSummary(summary);
            TransferStatus = T("BackupCenter.Status.ExportPreviewReady");
        });
    }

    [RelayCommand]
    private async Task ExportEncryptedAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(EncryptedExportPath))
        {
            TransferStatus = T("BackupCenter.Status.EnterEncryptedExportPath");
            return;
        }

        if (string.IsNullOrWhiteSpace(ExportPassphrase))
        {
            TransferStatus = T("BackupCenter.Status.EnterExportPassphrase");
            return;
        }

        await RunTransferAsync(async () =>
        {
            var summary = await EnsureExportSummaryAsync(vaultPath, vaultKey);
            await _transferService.ExportEncryptedAsync(vaultPath, vaultKey, EncryptedExportPath, ExportPassphrase);
            RecordHistory(OperationEncryptedBackup, "success", EncryptedExportPath, summary.ItemCount, summary.LabelCount);
            TransferStatus = T("BackupCenter.Status.EncryptedBackupSaved", FormatStatusPath(EncryptedExportPath));
            _root.LogActivity("transfer", "Encrypted backup exported", $"Saved an encrypted backup named {Path.GetFileName(EncryptedExportPath)}.", "success", vaultPath, Path.GetFileName(EncryptedExportPath));
        });
    }

    [RelayCommand]
    private async Task VerifyBackupAsync()
    {
        if (string.IsNullOrWhiteSpace(VerifyBackupPath))
        {
            TransferStatus = T("BackupCenter.Status.EnterVerifyPath");
            return;
        }

        if (string.IsNullOrWhiteSpace(VerifyPassphrase))
        {
            TransferStatus = T("BackupCenter.Status.EnterVerifyPassphrase");
            return;
        }

        await RunTransferAsync(async () =>
        {
            var summary = await _transferService.GetEncryptedImportSummaryAsync(VerifyBackupPath, VerifyPassphrase);
            VerifySummary = FormatImportSummary(summary);
            RecordHistory(OperationVerifyBackup, "success", VerifyBackupPath, summary.ItemCount, summary.LabelCount);
            TransferStatus = T("BackupCenter.Status.BackupVerified", FormatStatusPath(VerifyBackupPath));
            _root.LogActivity("transfer", "Backup verified", $"Verified encrypted backup named {Path.GetFileName(VerifyBackupPath)}.", "success", _root.VaultPath, Path.GetFileName(VerifyBackupPath));
        });
    }

    [RelayCommand]
    private async Task RestoreEncryptedAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(RestoreBackupPath))
        {
            TransferStatus = T("BackupCenter.Status.EnterRestorePath");
            return;
        }

        if (string.IsNullOrWhiteSpace(RestorePassphrase))
        {
            TransferStatus = T("BackupCenter.Status.EnterRestorePassphrase");
            return;
        }

        if (!ConfirmRestore)
        {
            TransferStatus = T("BackupCenter.Status.ConfirmRestore");
            return;
        }

        await RunTransferAsync(async () =>
        {
            await _root.ClearClipboardAsync();
            var summary = await _transferService.GetEncryptedImportSummaryAsync(RestoreBackupPath, RestorePassphrase);
            RestoreSummary = FormatImportSummary(summary);
            await _transferService.ImportEncryptedAsync(RestoreBackupPath, RestorePassphrase, vaultPath, vaultKey);
            RecordHistory(OperationRestoreBackup, "success", RestoreBackupPath, summary.ItemCount, summary.LabelCount);
            AutomaticBackupPassphrase = "";
            _root.ClearAutomaticBackupSessionPassphrase();
            _root.ReloadShell();
            TransferStatus = T("BackupCenter.Status.Restored");
            ConfirmRestore = false;
            _root.LogActivity("transfer", "Encrypted backup restored", $"Restored encrypted backup named {Path.GetFileName(RestoreBackupPath)}.", "success", vaultPath, Path.GetFileName(RestoreBackupPath));
        });
    }

    [RelayCommand]
    private async Task ExportPlaintextAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(PlaintextExportPath))
        {
            TransferStatus = T("BackupCenter.Status.EnterPlaintextPath");
            return;
        }

        if (!ConfirmPlaintextExport)
        {
            TransferStatus = T("BackupCenter.Status.ConfirmPlaintext");
            return;
        }

        if (!string.Equals(PlaintextExportConfirmationText.Trim(), "EXPORT", StringComparison.Ordinal))
        {
            TransferStatus = T("BackupCenter.Status.TypeExport");
            return;
        }

        await RunTransferAsync(async () =>
        {
            var summary = await EnsureExportSummaryAsync(vaultPath, vaultKey);
            await _transferService.ExportPlaintextJsonAsync(vaultPath, vaultKey, PlaintextExportPath);
            await _root.ClearClipboardAsync();
            RecordHistory(OperationPlaintextExport, "warning", PlaintextExportPath, summary.ItemCount, summary.LabelCount);
            TransferStatus = T("BackupCenter.Status.PlaintextExportSaved", FormatStatusPath(PlaintextExportPath));
            _root.LogActivity("transfer", "Plaintext export created", $"Saved a decrypted JSON export named {Path.GetFileName(PlaintextExportPath)}.", "warning", vaultPath, Path.GetFileName(PlaintextExportPath));
            ConfirmPlaintextExport = false;
            PlaintextExportConfirmationText = "";
        });
    }

    [RelayCommand]
    private async Task PreviewCsvImportAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(CsvImportPath))
        {
            TransferStatus = T("BackupCenter.Status.EnterCsvPath");
            return;
        }

        await RunTransferAsync(async () =>
        {
            await LoadCsvPreviewAsync(vaultPath, vaultKey);
            TransferStatus = T("BackupCenter.Status.CsvPreviewReady");
        });
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(CsvImportPath))
        {
            TransferStatus = T("BackupCenter.Status.EnterCsvPath");
            return;
        }

        await RunTransferAsync(async () =>
        {
            await _root.ClearClipboardAsync();
            if (CsvPreviewRows.Count == 0)
                await LoadCsvPreviewAsync(vaultPath, vaultKey);

            await _transferService.ImportCsvAsync(vaultPath, vaultKey, CsvImportPath, SelectedCsvDuplicateStrategy);
            RecordHistory(OperationCsvImport, "success", CsvImportPath, CsvPreviewRows.Count, 0);
            AutomaticBackupPassphrase = "";
            _root.ClearAutomaticBackupSessionPassphrase();
            _root.ReloadShell();
            TransferStatus = T("BackupCenter.Status.CsvImportFinished", SelectedCsvDuplicateStrategyOption?.Label ?? SelectedCsvDuplicateStrategy.ToString());
            _root.LogActivity("transfer", "CSV import completed", $"Imported items from {Path.GetFileName(CsvImportPath)} using {SelectedCsvDuplicateStrategy}.", "success", vaultPath, Path.GetFileName(CsvImportPath));
        });
    }

    private async Task<string?> PickBackupAsync(string title)
        => await _root.PickOpenFileAsync(title, [".skbx"], T("BackupCenter.Picker.ShellKryptBackup"));

    private bool TryEnsureUnlockedVault(out string vaultPath, out byte[] vaultKey)
    {
        vaultPath = "";
        vaultKey = [];

        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            TransferStatus = T("BackupCenter.Status.UnlockVault");
            return false;
        }

        vaultPath = _root.VaultPath;
        vaultKey = _root.VaultKey;
        return true;
    }

    private async Task RunTransferAsync(Func<Task> action)
    {
        IsTransferBusy = true;
        try
        {
            TransferStatus = "";
            await action();
        }
        catch (Exception ex)
        {
            TransferStatus = ex.Message;
        }
        finally
        {
            IsTransferBusy = false;
        }
    }

    private async Task<VaultSnapshotSummary> EnsureExportSummaryAsync(string vaultPath, byte[] vaultKey)
    {
        var summary = await _transferService.GetExportSummaryAsync(vaultPath, vaultKey);
        if (string.IsNullOrWhiteSpace(ExportSummary))
            ExportSummary = FormatExportSummary(summary);
        return summary;
    }

    private async Task LoadCsvPreviewAsync(string vaultPath, byte[] vaultKey)
    {
        var preview = await _transferService.PreviewCsvImportAsync(vaultPath, vaultKey, CsvImportPath);
        CsvPreviewRows.Clear();
        foreach (var row in preview.Rows)
            CsvPreviewRows.Add(row);

        CsvPreviewSummary = T("BackupCenter.Format.CsvSummary", preview.TotalRows, preview.NewRows, preview.DuplicateRows, preview.InvalidRows);
        OnPropertyChanged(nameof(HasCsvPreview));
    }

    private void RecordHistory(string operation, string status, string path, int itemCount, int labelCount)
    {
        var history = _root.BackupCenterHistory;
        var normalizedPath = string.IsNullOrWhiteSpace(path) ? "" : path.Trim();
        switch (operation)
        {
            case OperationEncryptedBackup:
                history.LastEncryptedBackupPath = normalizedPath;
                break;
            case OperationVerifyBackup:
                history.LastVerifiedBackupPath = normalizedPath;
                break;
            case OperationRestoreBackup:
                history.LastRestoredBackupPath = normalizedPath;
                break;
            case OperationPlaintextExport:
                history.LastPlaintextExportPath = normalizedPath;
                break;
            case OperationCsvImport:
                history.LastCsvImportPath = normalizedPath;
                break;
        }

        history.AddEntry(new BackupCenterHistoryEntry
        {
            Operation = operation,
            Status = status,
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            VaultName = GetVaultDisplayName(),
            FileName = Path.GetFileName(normalizedPath),
            FullPath = normalizedPath,
            ItemCount = itemCount,
            LabelCount = labelCount
        });

        _root.SaveBackupCenterHistory();
        RefreshHistoryRows();
        RefreshBackupHealth();
        NotifyLocalized(
            nameof(LastEncryptedBackupDisplay),
            nameof(LastVerifiedBackupDisplay),
            nameof(LastRestoreDisplay),
            nameof(LastPlaintextExportDisplay),
            nameof(LastCsvImportDisplay));
    }

    private void RefreshHistoryRows()
    {
        RecentHistory.Clear();
        foreach (var entry in _root.BackupCenterHistory.RecentEntries)
            RecentHistory.Add(new BackupHistoryEntryVm(entry, _root.Localization));

        OnPropertyChanged(nameof(HasRecentHistory));
    }

    private void LoadAutomaticBackupSettings()
    {
        _root.BackupSchedule.Normalize();
        AutomaticBackupEnabled = _root.BackupSchedule.Enabled;
        AutomaticBackupDirectory = _root.BackupSchedule.BackupDirectory;
        AutomaticBackupRetentionCount = _root.BackupSchedule.RetentionCount;
        SelectedAutomaticBackupFrequencyOption = AutomaticBackupFrequencyOptions.FirstOrDefault(option => option.Frequency == _root.BackupSchedule.Frequency)
            ?? AutomaticBackupFrequencyOptions.FirstOrDefault();
        RefreshAutomaticBackupState();
    }

    private void RefreshAutomaticBackupState()
    {
        if (!_root.AutomaticBackups.HasSessionPassphrase && !string.IsNullOrWhiteSpace(AutomaticBackupPassphrase))
            AutomaticBackupPassphrase = "";

        RefreshHistoryRows();
        OnPropertyChanged(nameof(HasAutomaticBackupSessionPassphrase));
        OnPropertyChanged(nameof(IsAutomaticBackupRunning));
        OnPropertyChanged(nameof(AutomaticBackupSessionText));
        OnPropertyChanged(nameof(LastAutomaticBackupDisplay));
        RefreshBackupHealth();
    }

    private void RefreshBackupHealth()
    {
        NotifyLocalized(
            nameof(RecoveryBackupExists),
            nameof(RecoveryBackupVerified),
            nameof(RecoveryAutomaticBackupConfigured),
            nameof(BackupHealthBackupStatus),
            nameof(BackupHealthVerificationStatus),
            nameof(BackupHealthAutomaticStatus),
            nameof(BackupHealthBackupDetail),
            nameof(BackupHealthVerificationDetail),
            nameof(BackupHealthAutomaticDetail),
            nameof(BackupHealthBackupBrush),
            nameof(BackupHealthVerificationBrush),
            nameof(BackupHealthAutomaticBrush),
            nameof(RecoveryLastBackupDisplay),
            nameof(RecoveryLastVerifiedDisplay),
            nameof(RecoveryAutomaticBackupDisplay));
    }

    private void RefreshEncryptedBackupModeState()
    {
        NotifyLocalized(
            nameof(IsCreateBackupMode),
            nameof(IsVerifyBackupMode),
            nameof(IsRestoreBackupMode),
            nameof(CreateBackupModeBackground),
            nameof(VerifyBackupModeBackground),
            nameof(RestoreBackupModeBackground),
            nameof(CreateBackupModeForeground),
            nameof(VerifyBackupModeForeground),
            nameof(RestoreBackupModeForeground));
    }

    private string FormatLastAutomaticBackup()
    {
        var state = _root.AutomaticBackupState;
        if (string.IsNullOrWhiteSpace(state.LastSuccessfulAtUtc))
            return _root.BackupSchedule.Enabled
                ? T("BackupCenter.Automatic.Status.EnabledNoRun")
                : T("BackupCenter.Automatic.Status.Disabled");

        var fileName = string.IsNullOrWhiteSpace(state.LastBackupFileName)
            ? Path.GetFileName(state.LastBackupPath)
            : state.LastBackupFileName;
        return T("BackupCenter.Format.LastOperation", fileName, FormatTimestamp(state.LastSuccessfulAtUtc));
    }

    private string BuildRecoveryAutomaticBackupDisplay()
    {
        if (!_root.BackupSchedule.Enabled)
            return T("BackupCenter.Health.Automatic.Disabled");

        if (string.IsNullOrWhiteSpace(_root.AutomaticBackupState.LastSuccessfulAtUtc))
            return T("BackupCenter.Health.Automatic.EnabledNoRun");

        return T("BackupCenter.Health.Format.LastAutomatic", _root.AutomaticBackupState.LastBackupFileName, FormatTimestamp(_root.AutomaticBackupState.LastSuccessfulAtUtc));
    }

    private string FormatLastKnownBackup()
    {
        if (!string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastEncryptedBackupPath))
            return FormatLastOperation(OperationEncryptedBackup, _root.BackupCenterHistory.LastEncryptedBackupPath);

        if (!string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastAutomaticBackupPath))
            return FormatLastOperation(AutomaticBackupCoordinator.OperationAutomaticBackup, _root.BackupCenterHistory.LastAutomaticBackupPath);

        return T("BackupCenter.Health.Status.NoBackup");
    }

    private string FormatLastKnownVerifiedBackup()
    {
        if (!string.IsNullOrWhiteSpace(_root.BackupCenterHistory.LastVerifiedBackupPath))
            return FormatLastOperation(OperationVerifyBackup, _root.BackupCenterHistory.LastVerifiedBackupPath);

        return T("BackupCenter.Health.Status.NoVerifiedBackup");
    }

    private string FormatExportSummary(VaultSnapshotSummary summary)
        => T(
            "BackupCenter.Format.ExportSummary",
            summary.ItemCount,
            summary.WebCount,
            summary.CardCount,
            summary.NoteCount,
            summary.AuthenticatorCount,
            summary.ApiKeyCount,
            summary.ProjectSecretCount,
            summary.LabelCount,
            summary.FavoriteCount);

    private string FormatImportSummary(VaultSnapshotSummary summary)
        => T(
            "BackupCenter.Format.ImportSummary",
            summary.ItemCount,
            summary.AuthenticatorCount,
            summary.ApiKeyCount,
            summary.ProjectSecretCount,
            summary.LabelCount,
            summary.FavoriteCount);

    private string FormatLastOperation(string operation, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return T("BackupCenter.Status.Never");

        var fileName = Path.GetFileName(path);
        var entry = _root.BackupCenterHistory.RecentEntries.FirstOrDefault(x => x.Operation == operation && string.Equals(x.FullPath, path, StringComparison.OrdinalIgnoreCase))
            ?? _root.BackupCenterHistory.RecentEntries.FirstOrDefault(x => x.Operation == operation);
        var timestamp = FormatTimestamp(entry?.TimestampUtc);
        return string.IsNullOrWhiteSpace(timestamp)
            ? fileName
            : T("BackupCenter.Format.LastOperation", fileName, timestamp);
    }

    private string FormatStatusPath(string path)
    {
        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName) ? path : fileName;
    }

    private string GetVaultDisplayName()
        => string.IsNullOrWhiteSpace(_root.VaultPath) ? T("BackupCenter.Status.NoActiveVault") : Path.GetFileNameWithoutExtension(_root.VaultPath);

    private string T(string key, params object[] args) => _root.Localization.Get(key, args);

    private static string UseHistoryOrDefault(string historyValue, string defaultValue)
        => string.IsNullOrWhiteSpace(historyValue) ? defaultValue : historyValue;

    private static string FirstNotEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static string FormatTimestamp(string? timestampUtc)
        => DateTimeOffset.TryParse(timestampUtc, out var parsed) ? parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "";

    private static CsvDuplicateStrategyOption[] CreateDuplicateStrategyOptions() =>
    [
        new(VaultCsvDuplicateStrategy.SkipDuplicates, "BackupCenter.Csv.Duplicate.Skip"),
        new(VaultCsvDuplicateStrategy.OverwriteDuplicates, "BackupCenter.Csv.Duplicate.Overwrite"),
        new(VaultCsvDuplicateStrategy.ImportAll, "BackupCenter.Csv.Duplicate.ImportAll")
    ];

    private static AutomaticBackupFrequencyOption[] CreateAutomaticBackupFrequencyOptions() =>
    [
        new(BackupScheduleFrequency.Daily, "BackupCenter.Automatic.Frequency.Daily"),
        new(BackupScheduleFrequency.EveryThreeDays, "BackupCenter.Automatic.Frequency.EveryThreeDays"),
        new(BackupScheduleFrequency.Weekly, "BackupCenter.Automatic.Frequency.Weekly")
    ];
}

public sealed partial class CsvDuplicateStrategyOption : ObservableObject
{
    public CsvDuplicateStrategyOption(VaultCsvDuplicateStrategy strategy, string labelKey)
    {
        Strategy = strategy;
        LabelKey = labelKey;
        Label = labelKey;
    }

    public VaultCsvDuplicateStrategy Strategy { get; }
    public string LabelKey { get; }
    [ObservableProperty] private string label = "";

    public void RefreshLocalization(LocalizationService localization)
    {
        Label = localization.Get(LabelKey);
    }

    public override string ToString() => Label;
}

public sealed partial class AutomaticBackupFrequencyOption : ObservableObject
{
    public AutomaticBackupFrequencyOption(BackupScheduleFrequency frequency, string labelKey)
    {
        Frequency = frequency;
        LabelKey = labelKey;
        Label = labelKey;
    }

    public BackupScheduleFrequency Frequency { get; }
    public string LabelKey { get; }
    [ObservableProperty] private string label = "";

    public void RefreshLocalization(LocalizationService localization)
    {
        Label = localization.Get(LabelKey);
    }

    public override string ToString() => Label;
}

public sealed partial class BackupHistoryEntryVm : ObservableObject
{
    private readonly BackupCenterHistoryEntry _entry;
    private readonly LocalizationService _localization;

    public BackupHistoryEntryVm(BackupCenterHistoryEntry entry, LocalizationService localization)
    {
        _entry = entry;
        _localization = localization;
    }

    public string OperationLabel => _entry.Operation switch
    {
        "encrypted-backup" => T("BackupCenter.Operation.EncryptedBackup"),
        "verify-backup" => T("BackupCenter.Operation.VerifyBackup"),
        "automatic-backup" => T("BackupCenter.Operation.AutomaticBackup"),
        "restore-backup" => T("BackupCenter.Operation.RestoreBackup"),
        "plaintext-export" => T("BackupCenter.Operation.PlaintextExport"),
        "csv-import" => T("BackupCenter.Operation.CsvImport"),
        "emergency-kit-export" => T("BackupCenter.Operation.LegacyExport"),
        _ => _entry.Operation
    };

    public string StatusLabel => _entry.Status switch
    {
        "success" => T("BackupCenter.StatusChip.Success"),
        "warning" => T("BackupCenter.StatusChip.Warning"),
        "error" => T("BackupCenter.StatusChip.Error"),
        _ => _entry.Status
    };

    public string TimestampDisplay => DateTimeOffset.TryParse(_entry.TimestampUtc, out var parsed)
        ? parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
        : _entry.TimestampUtc;

    public string FileDisplay => string.IsNullOrWhiteSpace(_entry.FileName) ? T("BackupCenter.Status.NoFile") : _entry.FileName;
    public string FullPathDisplay => _entry.FullPath;
    public string CountsDisplay => _entry.ItemCount > 0 || _entry.LabelCount > 0
        ? T("BackupCenter.History.Counts", _entry.ItemCount, _entry.LabelCount)
        : T("BackupCenter.History.NoCounts");

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(OperationLabel));
        OnPropertyChanged(nameof(StatusLabel));
        OnPropertyChanged(nameof(FileDisplay));
        OnPropertyChanged(nameof(CountsDisplay));
    }

    private string T(string key, params object[] args) => _localization.Get(key, args);
}
