using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
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
            _root.LogActivity("transfer", "Encrypted backup exported", $"Saved an encrypted backup named {Path.GetFileName(EncryptedExportPath)}.", "success", vaultPath, Path.GetFileName(EncryptedExportPath));
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

        if (!string.Equals(PlaintextExportConfirmationText.Trim(), "EXPORT", StringComparison.Ordinal))
        {
            TransferStatus = "Type EXPORT to confirm this decrypted JSON export.";
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
            TransferStatus = $"Plaintext JSON export saved to {Path.GetFileName(PlaintextExportPath)}. This file is decrypted; protect it and delete it when finished.";
            _root.LogActivity("transfer", "Plaintext export created", $"Saved a decrypted JSON export named {Path.GetFileName(PlaintextExportPath)}.", "warning", vaultPath, Path.GetFileName(PlaintextExportPath));
            ConfirmPlaintextExport = false;
            PlaintextExportConfirmationText = "";
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
            await _root.ClearClipboardAsync();
            if (string.IsNullOrWhiteSpace(EncryptedImportSummary))
            {
                var summary = await _transferService.GetEncryptedImportSummaryAsync(EncryptedImportPath, EncryptedImportPassphrase);
                EncryptedImportSummary = FormatImportSummary(summary);
            }

            await _transferService.ImportEncryptedAsync(EncryptedImportPath, EncryptedImportPassphrase, vaultPath, vaultKey);
            _root.ReloadShell();
            TransferStatus = "Encrypted backup restored into the current vault.";
            _root.LogActivity("transfer", "Encrypted backup imported", $"Restored an encrypted backup named {Path.GetFileName(EncryptedImportPath)}.", "success", vaultPath, Path.GetFileName(EncryptedImportPath));
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
