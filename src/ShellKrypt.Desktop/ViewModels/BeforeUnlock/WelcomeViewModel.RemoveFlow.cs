using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel
{
    [RelayCommand]
    private async Task RemoveSelectedVaultAsync()
    {
        Error = "";

        if (SelectedVault is null)
        {
            Error = T(_root, "Welcome.Status.SelectVaultFirst");
            return;
        }

        var confirmed = await _root.ConfirmDangerousActionAsync(
            T(_root, "Welcome.Remove.Title"),
            T(_root, "Welcome.Remove.ConfirmSubtitle", SelectedVault.DisplayLabel),
            T(_root, "Welcome.Remove.ConfirmDetail"),
            T(_root, "Common.RemoveFromList"));

        if (!confirmed)
            return;

        try
        {
            var displayName = SelectedVault.DisplayLabel;
            var path = SelectedVault.VaultPath;

            if (!_vaultRegistry.RemoveVault(path))
            {
                Error = T(_root, "Welcome.Error.VaultNoLongerRegistered");
                return;
            }

            if (string.Equals(_root.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            ReloadVaults();
            Status = T(_root, "Welcome.Remove.StatusRemoved", displayName);
            _root.LogActivity("vault", "Vault removed from launcher", $"Removed {displayName} from the local vault list.", "warning", path, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void RemoveVaultFromList(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = T(_root, "Welcome.Status.SelectVaultFirst");
            return;
        }

        RemoveTarget = vault;
        IsRemoveOverlayOpen = true;
    }

    [RelayCommand]
    private void CancelRemoveOverlay()
    {
        if (IsBusy)
            return;

        IsRemoveOverlayOpen = false;
        RemoveTarget = null;
    }

    [RelayCommand]
    private void ConfirmRemoveOverlay()
    {
        Error = "";

        var vault = RemoveTarget;
        if (vault is null)
        {
            IsRemoveOverlayOpen = false;
            return;
        }

        try
        {
            var displayName = vault.DisplayLabel;
            var path = vault.VaultPath;

            if (!_vaultRegistry.RemoveVault(path))
            {
                Error = T(_root, "Welcome.Error.VaultNoLongerRegistered");
                return;
            }

            if (string.Equals(_root.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            IsRemoveOverlayOpen = false;
            RemoveTarget = null;
            ReloadVaults();
            Status = T(_root, "Welcome.Remove.StatusRemoved", displayName);
            _root.LogActivity("vault", "Vault removed from launcher", $"Removed {displayName} from the local vault list.", "warning", path, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
