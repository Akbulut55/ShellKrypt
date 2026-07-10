using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    partial void OnMasterPasswordStatusChanged(string value) => OnPropertyChanged(nameof(HasMasterPasswordStatus));

    [RelayCommand]
    private async Task ChangeMasterPasswordAsync()
    {
        MasterPasswordStatus = "";

        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            MasterPasswordStatus = T("Settings.Status.ChangeMasterPasswordNoVault");
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentMasterPassword))
        {
            MasterPasswordStatus = T("Settings.Status.ChangeMasterPasswordEnterCurrent");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewMasterPassword))
        {
            MasterPasswordStatus = T("Settings.Status.ChangeMasterPasswordEnterNew");
            return;
        }

        if (string.Equals(CurrentMasterPassword, NewMasterPassword, StringComparison.Ordinal))
        {
            MasterPasswordStatus = T("Settings.Status.ChangeMasterPasswordDifferent");
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
            MasterPasswordStatus = T("Settings.Status.ChangeMasterPasswordMismatch");
            return;
        }

        await RunSettingsOperationAsync(async () =>
        {
            var result = await _vaultService.ChangeMasterPasswordAsync(
                _root.VaultPath!,
                CurrentMasterPassword,
                NewMasterPassword,
                SelectedSecurityProfile?.Kdf);

            if (!result.Success)
            {
                MasterPasswordStatus = result.Error ?? T("Settings.Status.ChangeMasterPasswordUnable");
                return;
            }

            CurrentMasterPassword = "";
            NewMasterPassword = "";
            ConfirmNewMasterPassword = "";
            MasterPasswordStatus = T("Settings.Status.ChangeMasterPasswordSuccess");
            Status = T("Settings.Status.ChangeMasterPasswordTransferSuccess");
            _root.LogActivity("vault", "Master password changed", $"Updated the master password for {GetVaultDisplayName()}.", "success", _root.VaultPath, GetVaultDisplayName());
            await LoadCurrentSecurityProfileAsync();
        });
    }
}
