using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Features.BackupCenter;

public sealed partial class PlaintextExportViewModel : ViewModelBase
{
    private readonly BackupCenterContext _context;
    private readonly BackupOperationState _operation;
    private readonly BackupHistoryViewModel _history;

    [ObservableProperty] private string exportPath = "";
    [ObservableProperty] private bool confirmExport;
    [ObservableProperty] private string confirmationText = "";

    internal PlaintextExportViewModel(
        BackupCenterContext context,
        BackupOperationState operation,
        BackupHistoryViewModel history)
    {
        _context = context;
        _operation = operation;
        _history = history;
        ExportPath = string.IsNullOrWhiteSpace(context.History.LastPlaintextExportPath)
            ? context.Files.GetSuggestedExportPath($"{context.VaultDisplayName} DECRYPTED Plaintext Export", ".json")
            : context.History.LastPlaintextExportPath;
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var path = await _context.PickSaveFileAsync(
            T("BackupCenter.Picker.Plaintext.SaveTitle"),
            Path.GetFileNameWithoutExtension(ExportPath),
            ".json",
            [".json"],
            T("BackupCenter.Picker.JsonExport"));
        if (!string.IsNullOrWhiteSpace(path))
            ExportPath = path;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (!_context.TryGetUnlockedVault(_operation, out var vaultPath, out var vaultKey))
            return;
        if (string.IsNullOrWhiteSpace(ExportPath))
        {
            _operation.Status = T("BackupCenter.Status.EnterPlaintextPath");
            return;
        }
        if (!ConfirmExport)
        {
            _operation.Status = T("BackupCenter.Status.ConfirmPlaintext");
            return;
        }
        if (!string.Equals(ConfirmationText.Trim(), "EXPORT", StringComparison.Ordinal))
        {
            _operation.Status = T("BackupCenter.Status.TypeExport");
            return;
        }

        await _operation.RunAsync(async () =>
        {
            var summary = await _context.Backups.GetSummaryAsync(vaultPath, vaultKey);
            await _context.PlaintextExports.ExportJsonAsync(vaultPath, vaultKey, ExportPath);
            await _context.ClearClipboardAsync();
            _history.Record(BackupHistoryViewModel.PlaintextExport, "warning", ExportPath, summary.ItemCount, summary.LabelCount);
            _operation.Status = T("BackupCenter.Status.PlaintextExportSaved", Path.GetFileName(ExportPath));
            _context.LogActivity(
                "transfer",
                "Plaintext export created",
                $"Saved a decrypted JSON export named {Path.GetFileName(ExportPath)}.",
                "warning",
                vaultPath,
                Path.GetFileName(ExportPath));
            ConfirmExport = false;
            ConfirmationText = "";
        });
    }

    private string T(string key, params object[] args) => _context.T(key, args);
}
