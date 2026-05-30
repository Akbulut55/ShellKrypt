using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private async Task ChangeMasterPasswordAsync()
    {
        MasterPasswordStatus = "";

        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            MasterPasswordStatus = "Unlock a vault before changing the master password.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentMasterPassword))
        {
            MasterPasswordStatus = "Enter the current master password.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewMasterPassword))
        {
            MasterPasswordStatus = "Enter a new master password.";
            return;
        }

        if (string.Equals(CurrentMasterPassword, NewMasterPassword, StringComparison.Ordinal))
        {
            MasterPasswordStatus = "Choose a different new master password.";
            return;
        }

        var validation = VaultMasterPasswordPolicy.Validate(NewMasterPassword);
        if (!validation.IsValid)
        {
            MasterPasswordStatus = validation.Message;
            return;
        }

        if (!string.Equals(NewMasterPassword, ConfirmNewMasterPassword, StringComparison.Ordinal))
        {
            MasterPasswordStatus = "New master passwords do not match.";
            return;
        }

        await RunTransferAsync(async () =>
        {
            var result = await _vaultService.ChangeMasterPasswordAsync(
                _root.VaultPath!,
                CurrentMasterPassword,
                NewMasterPassword,
                SelectedSecurityProfile?.Kdf);

            if (!result.Success)
            {
                MasterPasswordStatus = result.Error ?? "Unable to change the master password.";
                return;
            }

            CurrentMasterPassword = "";
            NewMasterPassword = "";
            ConfirmNewMasterPassword = "";
            MasterPasswordStatus = "Master password updated. Existing vault contents were re-wrapped in place.";
            TransferStatus = "Master password changed successfully.";
            _root.LogActivity("vault", "Master password changed", $"Updated the master password for {GetVaultDisplayName()}.", "success", _root.VaultPath, GetVaultDisplayName());
            await LoadCurrentSecurityProfileAsync();
        });
    }
}
