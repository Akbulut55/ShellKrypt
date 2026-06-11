using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private async Task DestroyVaultAsync()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            TransferStatus = T("Settings.Status.NoActiveVault");
            return;
        }

        var vaultPath = VaultFileGuard.EnsureSafeVaultDeletionTarget(_root.VaultPath!, _root.VaultPath);
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
                TransferStatus = unlockResult.Error ?? T("Settings.Status.WrongMasterPassword");
                return;
            }

            if (unlockResult.VaultKey is { Length: > 0 } vaultKeyBytes)
                CryptographicOperations.ZeroMemory(vaultKeyBytes);

            SqliteConnection.ClearAllPools();

            await _root.ClearClipboardAsync();
            VaultFileGuard.DeleteVaultAndKnownSidecars(vaultPath, _root.VaultPath);
            _vaultRegistry.RemoveVault(vaultPath);
            _root.SetVaultPath("");
            _root.Lock();
        });
    }
}
