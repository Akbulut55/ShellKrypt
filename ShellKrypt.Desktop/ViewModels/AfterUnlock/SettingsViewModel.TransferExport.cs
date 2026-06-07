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
}
