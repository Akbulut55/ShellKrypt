using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
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
}
