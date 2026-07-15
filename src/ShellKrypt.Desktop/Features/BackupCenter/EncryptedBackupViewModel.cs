using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Backups;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Infrastructure.Services;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed partial class EncryptedBackupViewModel : ViewModelBase
{
    private const string CreateMode = "create";
    private const string VerifyMode = "verify";
    private const string RestoreMode = "restore";
    private readonly BackupCenterContext _context;
    private readonly BackupOperationState _operation;
    private readonly BackupHistoryViewModel _history;

    [ObservableProperty] private string exportPath = "";
    [ObservableProperty] private string exportPassphrase = "";
    [ObservableProperty] private string exportSummary = "";
    [ObservableProperty] private string verifyPath = "";
    [ObservableProperty] private string verifyPassphrase = "";
    [ObservableProperty] private string verifySummary = "";
    [ObservableProperty] private string restorePath = "";
    [ObservableProperty] private string restorePassphrase = "";
    [ObservableProperty] private string restoreSummary = "";
    [ObservableProperty] private bool confirmRestore;
    [ObservableProperty] private string mode = CreateMode;

    internal EncryptedBackupViewModel(BackupCenterContext context, BackupOperationState operation, BackupHistoryViewModel history)
    {
        _context = context;
        _operation = operation;
        _history = history;
        var baseName = context.VaultDisplayName;
        ExportPath = UseHistoryOrDefault(context.History.LastEncryptedBackupPath, DefaultPaths.GetSuggestedExportPath($"{baseName} Backup", ".skbx"));
        VerifyPath = FirstNotEmpty(context.History.LastVerifiedBackupPath, context.History.LastEncryptedBackupPath);
        RestorePath = FirstNotEmpty(context.History.LastRestoredBackupPath, context.History.LastVerifiedBackupPath, context.History.LastEncryptedBackupPath);
    }

    public bool IsCreateMode => Mode == CreateMode;
    public bool IsVerifyMode => Mode == VerifyMode;
    public bool IsRestoreMode => Mode == RestoreMode;
    public string CreateModeBackground => IsCreateMode ? "AccentMutedBrush" : "SurfaceRaisedBrush";
    public string VerifyModeBackground => IsVerifyMode ? "AccentMutedBrush" : "SurfaceRaisedBrush";
    public string RestoreModeBackground => IsRestoreMode ? "AccentMutedBrush" : "SurfaceRaisedBrush";
    public string CreateModeForeground => IsCreateMode ? "AccentBrush" : "TextMutedBrush";
    public string VerifyModeForeground => IsVerifyMode ? "AccentBrush" : "TextMutedBrush";
    public string RestoreModeForeground => IsRestoreMode ? "AccentBrush" : "TextMutedBrush";

    partial void OnModeChanged(string value) => NotifyLocalized(
        nameof(IsCreateMode), nameof(IsVerifyMode), nameof(IsRestoreMode), nameof(CreateModeBackground),
        nameof(VerifyModeBackground), nameof(RestoreModeBackground), nameof(CreateModeForeground),
        nameof(VerifyModeForeground), nameof(RestoreModeForeground));

    [RelayCommand] private void ShowCreate() => Mode = CreateMode;
    [RelayCommand] private void ShowVerify() => Mode = VerifyMode;
    [RelayCommand] private void ShowRestore() => Mode = RestoreMode;

    [RelayCommand]
    private async Task BrowseExportPathAsync()
    {
        var path = await _context.PickSaveFileAsync(T("BackupCenter.Picker.EncryptedBackup.SaveTitle"), Path.GetFileNameWithoutExtension(ExportPath), ".skbx", [".skbx"], T("BackupCenter.Picker.ShellKryptBackup"));
        if (!string.IsNullOrWhiteSpace(path)) ExportPath = path;
    }

    [RelayCommand] private async Task BrowseVerifyPathAsync() => VerifyPath = await PickBackupAsync(T("BackupCenter.Picker.VerifyBackup.Title")) ?? VerifyPath;
    [RelayCommand] private async Task BrowseRestorePathAsync() => RestorePath = await PickBackupAsync(T("BackupCenter.Picker.RestoreBackup.Title")) ?? RestorePath;

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (!_context.TryGetUnlockedVault(_operation, out var vaultPath, out var vaultKey)) return;
        await _operation.RunAsync(async () =>
        {
            ExportSummary = FormatExportSummary(await _context.Backups.GetSummaryAsync(vaultPath, vaultKey));
            _operation.Status = T("BackupCenter.Status.ExportPreviewReady");
        });
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (!_context.TryGetUnlockedVault(_operation, out var vaultPath, out var vaultKey)) return;
        if (string.IsNullOrWhiteSpace(ExportPath)) { _operation.Status = T("BackupCenter.Status.EnterEncryptedExportPath"); return; }
        if (string.IsNullOrWhiteSpace(ExportPassphrase)) { _operation.Status = T("BackupCenter.Status.EnterExportPassphrase"); return; }
        await _operation.RunAsync(async () =>
        {
            var summary = await _context.Backups.GetSummaryAsync(vaultPath, vaultKey);
            if (string.IsNullOrWhiteSpace(ExportSummary)) ExportSummary = FormatExportSummary(summary);
            await _context.Backups.CreateAsync(vaultPath, vaultKey, ExportPath, ExportPassphrase);
            _history.Record(BackupHistoryViewModel.EncryptedBackup, "success", ExportPath, summary.ItemCount, summary.LabelCount);
            _operation.Status = T("BackupCenter.Status.EncryptedBackupSaved", Path.GetFileName(ExportPath));
            _context.LogActivity("transfer", "Encrypted backup exported", $"Saved an encrypted backup named {Path.GetFileName(ExportPath)}.", "success", vaultPath, Path.GetFileName(ExportPath));
        });
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        if (string.IsNullOrWhiteSpace(VerifyPath)) { _operation.Status = T("BackupCenter.Status.EnterVerifyPath"); return; }
        if (string.IsNullOrWhiteSpace(VerifyPassphrase)) { _operation.Status = T("BackupCenter.Status.EnterVerifyPassphrase"); return; }
        await _operation.RunAsync(async () =>
        {
            var summary = await _context.Backups.InspectAsync(VerifyPath, VerifyPassphrase);
            VerifySummary = FormatImportSummary(summary);
            _history.Record(BackupHistoryViewModel.VerifyBackup, "success", VerifyPath, summary.ItemCount, summary.LabelCount);
            _operation.Status = T("BackupCenter.Status.BackupVerified", Path.GetFileName(VerifyPath));
            _context.LogActivity("transfer", "Backup verified", $"Verified encrypted backup named {Path.GetFileName(VerifyPath)}.", "success", _context.VaultPath, Path.GetFileName(VerifyPath));
        });
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (!_context.TryGetUnlockedVault(_operation, out var vaultPath, out var vaultKey)) return;
        if (string.IsNullOrWhiteSpace(RestorePath)) { _operation.Status = T("BackupCenter.Status.EnterRestorePath"); return; }
        if (string.IsNullOrWhiteSpace(RestorePassphrase)) { _operation.Status = T("BackupCenter.Status.EnterRestorePassphrase"); return; }
        if (!ConfirmRestore) { _operation.Status = T("BackupCenter.Status.ConfirmRestore"); return; }
        await _operation.RunAsync(async () =>
        {
            await _context.ClearClipboardAsync();
            var summary = await _context.Backups.InspectAsync(RestorePath, RestorePassphrase);
            RestoreSummary = FormatImportSummary(summary);
            await _context.Backups.RestoreAsync(RestorePath, RestorePassphrase, vaultPath, vaultKey);
            _history.Record(BackupHistoryViewModel.RestoreBackup, "success", RestorePath, summary.ItemCount, summary.LabelCount);
            _context.ClearAutomaticBackupPassphrase();
            _context.ReloadShell();
            _operation.Status = T("BackupCenter.Status.Restored");
            ConfirmRestore = false;
            _context.LogActivity("transfer", "Encrypted backup restored", $"Restored encrypted backup named {Path.GetFileName(RestorePath)}.", "success", vaultPath, Path.GetFileName(RestorePath));
        });
    }

    private Task<string?> PickBackupAsync(string title) => _context.PickOpenFileAsync(title, [".skbx"], T("BackupCenter.Picker.ShellKryptBackup"));
    private string FormatExportSummary(VaultSnapshotSummary s) => T("BackupCenter.Format.ExportSummary", s.ItemCount, s.WebCount, s.CardCount, s.NoteCount, s.AuthenticatorCount, s.ApiKeyCount, s.ProjectSecretCount, s.LabelCount, s.FavoriteCount);
    private string FormatImportSummary(VaultSnapshotSummary s) => T("BackupCenter.Format.ImportSummary", s.ItemCount, s.AuthenticatorCount, s.ApiKeyCount, s.ProjectSecretCount, s.LabelCount, s.FavoriteCount);
    private string T(string key, params object[] args) => _context.T(key, args);
    private static string UseHistoryOrDefault(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    private static string FirstNotEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}
