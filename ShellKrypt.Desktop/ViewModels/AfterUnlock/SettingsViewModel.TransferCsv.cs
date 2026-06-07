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
            await _root.ClearClipboardAsync();
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
            _root.LogActivity("transfer", "CSV import completed", $"Imported items from {Path.GetFileName(CsvImportPath)} using {SelectedCsvDuplicateStrategy}.", "success", vaultPath, Path.GetFileName(CsvImportPath));
        });
    }
}
