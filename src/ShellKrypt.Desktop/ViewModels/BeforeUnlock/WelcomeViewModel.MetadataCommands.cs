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
                markOpened: false);

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
    private void ToggleFavorite(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = T(_root, "Welcome.Status.SelectVaultFirst");
            return;
        }

        try
        {
            var isFavorite = !vault.IsFavorite;
            _vaultRegistry.SetVaultFavorite(vault.VaultPath, isFavorite);
            ReloadVaults(vault.VaultPath);
            Status = T(_root, isFavorite ? "Welcome.Status.FavoriteVaultAdded" : "Welcome.Status.FavoriteVaultRemoved");
            _root.LogActivity("vault", "Vault favorite changed", $"{vault.DisplayLabel} favorite status changed.", "info", vault.VaultPath, vault.DisplayLabel);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
