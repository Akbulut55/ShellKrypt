using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private async Task PreviewCsvImportAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(CsvImportPath))
        {
            TransferStatus = T("Settings.Status.TransferEnterCsvPath");
            return;
        }

        await RunTransferAsync(async () =>
        {
            var preview = await _transferService.PreviewCsvImportAsync(vaultPath, vaultKey, CsvImportPath);
            CsvPreviewRows.Clear();
            foreach (var row in preview.Rows)
                CsvPreviewRows.Add(row);

            CsvPreviewSummary = T("Settings.Format.CsvSummary", preview.TotalRows, preview.NewRows, preview.DuplicateRows, preview.InvalidRows);
            OnPropertyChanged(nameof(HasCsvPreview));
            TransferStatus = T("Settings.Status.CsvPreviewReady");
        });
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(CsvImportPath))
        {
            TransferStatus = T("Settings.Status.TransferEnterCsvPath");
            return;
        }

        await RunTransferAsync(async () =>
        {
            await _root.ClearClipboardAsync();
            if (CsvPreviewRows.Count == 0)
            {
                var preview = await _transferService.PreviewCsvImportAsync(vaultPath, vaultKey, CsvImportPath);
                CsvPreviewRows.Clear();
                foreach (var row in preview.Rows)
                    CsvPreviewRows.Add(row);

                CsvPreviewSummary = T("Settings.Format.CsvSummary", preview.TotalRows, preview.NewRows, preview.DuplicateRows, preview.InvalidRows);
            }

            await _transferService.ImportCsvAsync(vaultPath, vaultKey, CsvImportPath, SelectedCsvDuplicateStrategy);
            _root.ReloadShell();
            TransferStatus = T("Settings.Status.CsvImportFinished", SelectedCsvDuplicateStrategy);
            _root.LogActivity("transfer", "CSV import completed", $"Imported items from {Path.GetFileName(CsvImportPath)} using {SelectedCsvDuplicateStrategy}.", "success", vaultPath, Path.GetFileName(CsvImportPath));
        });
    }
}
