using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel
{
    [RelayCommand]
    private async Task EditVaultAsync(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = T(_root, "Welcome.Status.SelectVaultFirst");
            return;
        }

        try
        {
            var (confirmed, displayName, description) = await _root.ShowEditVaultDialogAsync(
                vault.DisplayName,
                vault.Description,
                vault.VaultPath);

            if (!confirmed)
                return;

            _vaultRegistry.UpsertVault(
                vault.VaultPath,
                displayName,
                description,
                vault.IsDefault);

            ReloadVaults(vault.VaultPath);
        Status = T(_root, "Welcome.Status.MetadataSaved");
            _root.LogActivity("vault", "Vault metadata updated", $"Updated metadata for {displayName}.", "info", vault.VaultPath, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void MakeDefault(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = T(_root, "Welcome.Status.SelectVaultFirst");
            return;
        }

        try
        {
            _vaultRegistry.SetDefaultVault(vault.VaultPath);
            ReloadVaults(vault.VaultPath);
        Status = T(_root, "Welcome.Status.DefaultVaultUpdated");
            _root.LogActivity("vault", "Default vault changed", $"Marked {vault.DisplayLabel} as the default vault.", "info", vault.VaultPath, vault.DisplayLabel);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
