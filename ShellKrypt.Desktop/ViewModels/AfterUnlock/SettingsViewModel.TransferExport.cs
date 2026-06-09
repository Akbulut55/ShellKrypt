using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private async Task PreviewExportAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        await RunTransferAsync(async () =>
        {
            var summary = await _transferService.GetExportSummaryAsync(vaultPath, vaultKey);
            ExportSummary = FormatExportSummary(summary);
            TransferStatus = T("Settings.Status.ExportPreviewReady");
        });
    }

    [RelayCommand]
    private async Task ExportEncryptedAsync()
    {
        if (!TryEnsureUnlockedVault(out var vaultPath, out var vaultKey))
            return;

        if (string.IsNullOrWhiteSpace(EncryptedExportPath))
        {
            TransferStatus = T("Settings.Status.TransferEnterEncryptedExportPath");
            return;
        }

        if (string.IsNullOrWhiteSpace(ExportPassphrase))
        {
            TransferStatus = T("Settings.Status.TransferEnterExportPassphrase");
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
            TransferStatus = T("Settings.Status.EncryptedBackupSaved", EncryptedExportPath);
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
            TransferStatus = T("Settings.Status.TransferEnterPlaintextPath");
            return;
        }

        if (!ConfirmPlaintextExport)
        {
            TransferStatus = T("Settings.Status.TransferConfirmPlaintext");
            return;
        }

        if (!string.Equals(PlaintextExportConfirmationText.Trim(), "EXPORT", StringComparison.Ordinal))
        {
            TransferStatus = T("Settings.Status.TransferTypeExport");
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
            TransferStatus = T("Settings.Status.PlaintextExportSaved", Path.GetFileName(PlaintextExportPath));
            _root.LogActivity("transfer", "Plaintext export created", $"Saved a decrypted JSON export named {Path.GetFileName(PlaintextExportPath)}.", "warning", vaultPath, Path.GetFileName(PlaintextExportPath));
            ConfirmPlaintextExport = false;
            PlaintextExportConfirmationText = "";
        });
    }
}
