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
            Error = T(_localization, "Welcome.Status.SelectVaultFirst");
            return;
        }

        try
        {
            var (confirmed, displayName, description) = await _dialogs.ShowEditVaultDialogAsync(
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
        Status = T(_localization, "Welcome.Status.MetadataSaved");
            _activity.Log("vault", "Vault metadata updated", $"Updated metadata for {displayName}.", "info", vault.VaultPath, displayName);
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
            Error = T(_localization, "Welcome.Status.SelectVaultFirst");
            return;
        }

        try
        {
            var isFavorite = !vault.IsFavorite;
            _vaultRegistry.SetVaultFavorite(vault.VaultPath, isFavorite);
            ReloadVaults(vault.VaultPath);
            Status = T(_localization, isFavorite ? "Welcome.Status.FavoriteVaultAdded" : "Welcome.Status.FavoriteVaultRemoved");
            _activity.Log("vault", "Vault favorite changed", $"{vault.DisplayLabel} favorite status changed.", "info", vault.VaultPath, vault.DisplayLabel);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
