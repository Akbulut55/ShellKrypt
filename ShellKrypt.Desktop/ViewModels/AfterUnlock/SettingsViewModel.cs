using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    public sealed class AutoLockDurationOption
    {
        public AutoLockDurationOption(int minutes, string label)
        {
            Minutes = minutes;
            Label = label;
        }

        public int Minutes { get; }
        public string Label { get; }
    }

    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IVaultTransferService _transferService = new SqliteVaultTransferService();
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly VaultRegistryStore _vaultRegistry = new();

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private AutoLockDurationOption? selectedAutoLockDuration;
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private string lockOnDeactivateSecondsText = "";
    [ObservableProperty] private string clipboardClearSecondsText = "";
    [ObservableProperty] private double clipboardClearSecondsValue;
    [ObservableProperty] private AppThemeMode selectedThemeMode;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string transferStatus = "Preview an operation before applying it.";
    [ObservableProperty] private bool isTransferBusy;

    [ObservableProperty] private string encryptedExportPath = "";
    [ObservableProperty] private string exportPassphrase = "";
    [ObservableProperty] private string exportSummary = "";

    [ObservableProperty] private string plaintextExportPath = "";
    [ObservableProperty] private bool confirmPlaintextExport;

    [ObservableProperty] private string encryptedImportPath = "";
    [ObservableProperty] private string encryptedImportPassphrase = "";
    [ObservableProperty] private string encryptedImportSummary = "";

    [ObservableProperty] private string csvImportPath = "";
    [ObservableProperty] private VaultCsvDuplicateStrategy selectedCsvDuplicateStrategy = VaultCsvDuplicateStrategy.SkipDuplicates;
    [ObservableProperty] private string csvPreviewSummary = "";

    public ObservableCollection<VaultCsvDuplicateStrategy> CsvDuplicateStrategies { get; } =
    [
        VaultCsvDuplicateStrategy.SkipDuplicates,
        VaultCsvDuplicateStrategy.OverwriteDuplicates,
        VaultCsvDuplicateStrategy.ImportAll
    ];

    public ObservableCollection<AppThemeMode> ThemeModes { get; } =
    [
        AppThemeMode.Dark,
        AppThemeMode.Light
    ];

    public ObservableCollection<AutoLockDurationOption> AutoLockDurations { get; } =
    [
        new(1, "1 Minute"),
        new(5, "5 Minutes"),
        new(10, "10 Minutes"),
        new(15, "15 Minutes"),
        new(30, "30 Minutes"),
        new(60, "1 Hour"),
        new(120, "2 Hours"),
    ];

    public ObservableCollection<VaultCsvImportRowPreview> CsvPreviewRows { get; } = new();

    public SettingsViewModel(MainWindowViewModel root, ShellViewModel shell)
    {
        _root = root;
        _shell = shell;
        CsvPreviewRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCsvPreview));
        LoadFromRootSettings();
        Status = "Settings save automatically.";

        var exportBaseName = GetVaultDisplayName();
        EncryptedExportPath = DefaultPaths.GetSuggestedExportPath($"{exportBaseName} Backup", ".skbx");
        PlaintextExportPath = DefaultPaths.GetSuggestedExportPath($"{exportBaseName} Export", ".json");
    }

    public bool HasCsvPreview => CsvPreviewRows.Count > 0;
    public string ActiveVaultDisplay => GetVaultFileName();
    public string ActiveVaultPathDisplay => string.IsNullOrWhiteSpace(_root.VaultPath) ? "No active vault path." : _root.VaultPath;
    public string VaultStorageDisplay => GetVaultStorageDisplay();
    public double VaultStoragePercent => GetVaultStoragePercent();
    public string ClipboardClearSecondsDisplay => $"{ClipboardClearSecondsText}s";
    public string EncryptionDisplay => "AES-256";
    public string ThemeModeLabel => SelectedThemeMode == AppThemeMode.Dark ? "Dark" : "Light";
    public string FocusLockSummary => LockOnDeactivate
        ? $"The vault locks after the app stays out of focus for about {LockOnDeactivateSecondsText} seconds."
        : "Switching away from the app will not immediately lock the vault.";
    public string SecurityStatusText => AutoLockEnabled
        ? $"Auto-lock enabled • {SelectedAutoLockDuration?.Label ?? "Configured"}"
        : "Auto-lock disabled";
    public string IntegrityStatusText => "ShellKrypt uses zero-knowledge local encryption. Settings stay on-device.";

    partial void OnAutoLockEnabledChanged(bool value)
    {
        _root.AutoLockEnabled = value;
        OnPropertyChanged(nameof(SecurityStatusText));
    }

    partial void OnLockOnDeactivateChanged(bool value)
    {
        _root.LockOnDeactivate = value;
        OnPropertyChanged(nameof(FocusLockSummary));
    }

    partial void OnLockOnDeactivateSecondsTextChanged(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            Status = "Out-of-focus lock delay must be a whole number.";
            OnPropertyChanged(nameof(FocusLockSummary));
            return;
        }

        if (seconds < 1)
        {
            Status = "Out-of-focus lock delay must be at least 1 second.";
            OnPropertyChanged(nameof(FocusLockSummary));
            return;
        }

        _root.LockOnDeactivateSeconds = seconds;
        Status = "Settings saved.";
        OnPropertyChanged(nameof(FocusLockSummary));
    }

    partial void OnSelectedAutoLockDurationChanged(AutoLockDurationOption? value)
    {
        if (value is null)
            return;

        _root.AutoLockMinutes = value.Minutes;
        _root.AutoLockEnabled = value.Minutes > 0;
        Status = "Settings saved.";
        OnPropertyChanged(nameof(SecurityStatusText));
    }

    partial void OnSelectedThemeModeChanged(AppThemeMode value)
    {
        _root.ThemeMode = value;
        Status = $"Theme switched to {value}.";
        OnPropertyChanged(nameof(ThemeModeLabel));
    }

    partial void OnClipboardClearSecondsTextChanged(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            Status = "Clipboard timeout must be a whole number.";
            return;
        }

        if (seconds < 1)
        {
            Status = "Clipboard timeout must be at least 1 second.";
            return;
        }

        _root.ClipboardClearSeconds = seconds;
        if (Math.Abs(ClipboardClearSecondsValue - seconds) > double.Epsilon)
            ClipboardClearSecondsValue = seconds;
        Status = "Settings saved.";
    }

    partial void OnClipboardClearSecondsValueChanged(double value)
    {
        var seconds = Math.Clamp((int)Math.Round(value), 1, 120);
        var text = seconds.ToString(CultureInfo.InvariantCulture);

        if (!string.Equals(ClipboardClearSecondsText, text, StringComparison.Ordinal))
            ClipboardClearSecondsText = text;

        if (_root.ClipboardClearSeconds != seconds)
            _root.ClipboardClearSeconds = seconds;

        Status = "Settings saved.";
        OnPropertyChanged(nameof(ClipboardClearSecondsDisplay));
    }

    [RelayCommand]
    private async Task BrowseEncryptedExportPathAsync()
    {
        var path = await _root.PickSaveFileAsync(
            "Choose encrypted backup location",
            Path.GetFileNameWithoutExtension(EncryptedExportPath),
            ".skbx",
            [".skbx"],
            "ShellKrypt Backup");

        if (!string.IsNullOrWhiteSpace(path))
            EncryptedExportPath = path;
    }

    [RelayCommand]
    private async Task BrowsePlaintextExportPathAsync()
    {
        var path = await _root.PickSaveFileAsync(
            "Choose plaintext export location",
            Path.GetFileNameWithoutExtension(PlaintextExportPath),
            ".json",
            [".json"],
            "JSON Export");

        if (!string.IsNullOrWhiteSpace(path))
            PlaintextExportPath = path;
    }

    [RelayCommand]
    private async Task BrowseEncryptedImportPathAsync()
    {
        var path = await _root.PickOpenFileAsync(
            "Select encrypted backup",
            [".skbx"],
            "ShellKrypt Backup");

        if (!string.IsNullOrWhiteSpace(path))
            EncryptedImportPath = path;
    }

    [RelayCommand]
    private async Task BrowseCsvImportPathAsync()
    {
        var path = await _root.PickOpenFileAsync(
            "Select CSV import file",
            [".csv"],
            "CSV File");

        if (!string.IsNullOrWhiteSpace(path))
            CsvImportPath = path;
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
            TransferStatus = "Export preview is ready.";
        });
    }

    [RelayCommand]
    private async Task ExportEncryptedAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(EncryptedExportPath))
        {
            TransferStatus = "Enter an encrypted export path first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ExportPassphrase))
        {
            TransferStatus = "Enter an export passphrase first.";
            return;
        }

        await RunTransferAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(ExportSummary))
            {
                var summary = await _transferService.GetExportSummaryAsync(vaultPath, vaultKey);
                ExportSummary = FormatExportSummary(summary);
            }

            await _transferService.ExportEncryptedAsync(vaultPath, vaultKey, EncryptedExportPath, ExportPassphrase);
            TransferStatus = $"Encrypted backup saved to {EncryptedExportPath}.";
            _root.LogActivity("transfer", "Encrypted backup exported", $"Saved an encrypted backup to {EncryptedExportPath}.", "success", vaultPath);
        });
    }

    [RelayCommand]
    private async Task ExportPlaintextAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(PlaintextExportPath))
        {
            TransferStatus = "Enter a plaintext export path first.";
            return;
        }

        if (!ConfirmPlaintextExport)
        {
            TransferStatus = "Confirm the plaintext export warning before continuing.";
            return;
        }

        await RunTransferAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(ExportSummary))
            {
                var summary = await _transferService.GetExportSummaryAsync(vaultPath, vaultKey);
                ExportSummary = FormatExportSummary(summary);
            }

            await _transferService.ExportPlaintextJsonAsync(vaultPath, vaultKey, PlaintextExportPath);
            TransferStatus = $"Plaintext JSON export saved to {PlaintextExportPath}.";
            _root.LogActivity("transfer", "Plaintext export created", $"Saved a plaintext JSON export to {PlaintextExportPath}.", "warning", vaultPath);
        });
    }

    [RelayCommand]
    private async Task PreviewEncryptedImportAsync()
    {
        if (!TryEnsureUnlockedVault(out _, out _))
            return;

        if (string.IsNullOrWhiteSpace(EncryptedImportPath))
        {
            TransferStatus = "Enter an encrypted backup path first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EncryptedImportPassphrase))
        {
            TransferStatus = "Enter the import passphrase first.";
            return;
        }

        await RunTransferAsync(async () =>
        {
            var summary = await _transferService.GetEncryptedImportSummaryAsync(EncryptedImportPath, EncryptedImportPassphrase);
            EncryptedImportSummary = FormatImportSummary(summary);
            TransferStatus = "Encrypted restore preview is ready.";
        });
    }

    [RelayCommand]
    private async Task ImportEncryptedAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(EncryptedImportPath))
        {
            TransferStatus = "Enter an encrypted backup path first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EncryptedImportPassphrase))
        {
            TransferStatus = "Enter the import passphrase first.";
            return;
        }

        await RunTransferAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(EncryptedImportSummary))
            {
                var summary = await _transferService.GetEncryptedImportSummaryAsync(EncryptedImportPath, EncryptedImportPassphrase);
                EncryptedImportSummary = FormatImportSummary(summary);
            }

            await _transferService.ImportEncryptedAsync(EncryptedImportPath, EncryptedImportPassphrase, vaultPath, vaultKey);
            _root.ReloadShell();
            TransferStatus = "Encrypted backup restored into the current vault.";
            _root.LogActivity("transfer", "Encrypted backup imported", $"Restored an encrypted backup from {EncryptedImportPath}.", "success", vaultPath);
        });
    }

    [RelayCommand]
    private async Task PreviewCsvImportAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(CsvImportPath))
        {
            TransferStatus = "Enter a CSV file path first.";
            return;
        }

        await RunTransferAsync(async () =>
        {
            var preview = await _transferService.PreviewCsvImportAsync(vaultPath, vaultKey, CsvImportPath);
            CsvPreviewRows.Clear();
            foreach (var row in preview.Rows)
                CsvPreviewRows.Add(row);

            CsvPreviewSummary =
                $"Rows: {preview.TotalRows} | New: {preview.NewRows} | Duplicates: {preview.DuplicateRows} | Invalid: {preview.InvalidRows}";
            OnPropertyChanged(nameof(HasCsvPreview));
            TransferStatus = "CSV preview is ready.";
        });
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(CsvImportPath))
        {
            TransferStatus = "Enter a CSV file path first.";
            return;
        }

        await RunTransferAsync(async () =>
        {
            if (CsvPreviewRows.Count == 0)
            {
                var preview = await _transferService.PreviewCsvImportAsync(vaultPath, vaultKey, CsvImportPath);
                CsvPreviewRows.Clear();
                foreach (var row in preview.Rows)
                    CsvPreviewRows.Add(row);

                CsvPreviewSummary =
                    $"Rows: {preview.TotalRows} | New: {preview.NewRows} | Duplicates: {preview.DuplicateRows} | Invalid: {preview.InvalidRows}";
            }

            await _transferService.ImportCsvAsync(vaultPath, vaultKey, CsvImportPath, SelectedCsvDuplicateStrategy);
            _root.ReloadShell();
            TransferStatus = $"CSV import finished using {SelectedCsvDuplicateStrategy}.";
            _root.LogActivity("transfer", "CSV import completed", $"Imported items from {CsvImportPath} using {SelectedCsvDuplicateStrategy}.", "success", vaultPath);
        });
    }

    [RelayCommand]
    private void SaveChanges()
    {
        Status = "Changes saved locally.";
    }

    [RelayCommand]
    private void DiscardChanges()
    {
        LoadFromRootSettings();
        Status = "Local settings reloaded.";
    }

    [RelayCommand]
    private void ViewAudit()
    {
        _shell.ShowSecurityAudit();
    }

    [RelayCommand]
    private async Task DestroyVaultAsync()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            TransferStatus = "No active vault is selected.";
            return;
        }

        var vaultPath = _root.VaultPath!;
        var displayName = Path.GetFileNameWithoutExtension(vaultPath);

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Permanently Delete Vault?",
            $"Permanently delete {displayName}?",
            "Warning: this action is irreversible. All stored passwords, markdown notes, and encrypted data within this vault will be destroyed immediately.",
            "Permanently Delete");

        if (!confirmed)
            return;

        var password = await _root.PromptPasswordAsync(
            "Confirm Master Password",
            "Enter the master password to permanently delete this vault.",
            vaultPath,
            "Delete Vault");

        if (password is null)
            return;

        await RunTransferAsync(async () =>
        {
            var unlockResult = await _vaultService.UnlockAsync(vaultPath, password);
            if (!unlockResult.Success)
            {
                TransferStatus = unlockResult.Error ?? "Wrong master password.";
                return;
            }

            if (unlockResult.VaultKey is { Length: > 0 } vaultKeyBytes)
                Array.Clear(vaultKeyBytes, 0, vaultKeyBytes.Length);

            SqliteConnection.ClearAllPools();

            DeleteSidecarIfExists(vaultPath, "-wal");
            DeleteSidecarIfExists(vaultPath, "-shm");
            DeleteSidecarIfExists(vaultPath, "-journal");
            File.Delete(vaultPath);
            _vaultRegistry.RemoveVault(vaultPath);
            _root.LogActivity("vault", "Vault deleted", $"Permanently deleted {displayName}.", "danger", vaultPath);
            _root.Lock();
        });
    }

    private bool TryEnsureUnlockedVault(out string vaultPath, out byte[] vaultKey)
    {
        vaultPath = "";
        vaultKey = [];

        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            TransferStatus = "Unlock a vault before using import or export.";
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

    private string GetVaultDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return "Vault";

        return Path.GetFileNameWithoutExtension(_root.VaultPath);
    }

    private string GetVaultStorageDisplay()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath) || !File.Exists(_root.VaultPath))
            return "640 MB / 1 GB Storage used";

        var bytes = new FileInfo(_root.VaultPath).Length;
        return $"{FormatBytes(bytes)} / 1 GB Storage used";
    }

    private double GetVaultStoragePercent()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath) || !File.Exists(_root.VaultPath))
            return 64;

        const double oneGb = 1024d * 1024d * 1024d;
        var bytes = new FileInfo(_root.VaultPath).Length;
        return Math.Clamp(bytes / oneGb * 100d, 0d, 100d);
    }

    private string GetVaultFileName()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return "Personal_Vault_v2.skryp";

        return Path.GetFileName(_root.VaultPath);
    }

    private static string FormatBytes(long bytes)
    {
        const double kilobyte = 1024d;
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        if (bytes >= gigabyte)
            return $"{bytes / gigabyte:0.#} GB";

        if (bytes >= megabyte)
            return $"{bytes / megabyte:0.#} MB";

        if (bytes >= kilobyte)
            return $"{bytes / kilobyte:0.#} KB";

        return $"{bytes} B";
    }

    private static string FormatExportSummary(VaultSnapshotSummary summary)
        => $"Items: {summary.ItemCount} | Web: {summary.WebCount} | Cards: {summary.CardCount} | Notes: {summary.NoteCount} | Labels: {summary.LabelCount} | Favorites: {summary.FavoriteCount}";

    private static string FormatImportSummary(VaultSnapshotSummary summary)
        => $"Previewing import: {summary.ItemCount} items, {summary.LabelCount} labels, {summary.FavoriteCount} favorites.";

    private void LoadFromRootSettings()
    {
        AutoLockEnabled = _root.AutoLockEnabled;
        SelectedAutoLockDuration = ResolveAutoLockDuration(_root.AutoLockMinutes);
        LockOnDeactivate = _root.LockOnDeactivate;
        LockOnDeactivateSecondsText = _root.LockOnDeactivateSeconds.ToString(CultureInfo.InvariantCulture);
        ClipboardClearSecondsText = _root.ClipboardClearSeconds.ToString(CultureInfo.InvariantCulture);
        ClipboardClearSecondsValue = Math.Clamp(_root.ClipboardClearSeconds, 5, 120);
        SelectedThemeMode = _root.ThemeMode;
        OnPropertyChanged(nameof(SecurityStatusText));
        OnPropertyChanged(nameof(ThemeModeLabel));
        OnPropertyChanged(nameof(FocusLockSummary));
    }

    private AutoLockDurationOption ResolveAutoLockDuration(int minutes)
    {
        var existing = AutoLockDurations.FirstOrDefault(x => x.Minutes == minutes);
        if (existing is not null)
            return existing;

        var custom = new AutoLockDurationOption(minutes, $"{minutes} minutes");
        AutoLockDurations.Add(custom);
        return custom;
    }

    private static void DeleteSidecarIfExists(string vaultPath, string suffix)
    {
        var sidecar = vaultPath + suffix;
        if (File.Exists(sidecar))
            File.Delete(sidecar);
    }
}
