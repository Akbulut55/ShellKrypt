using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.Features.VaultAccess;

public sealed partial class WelcomeViewModel
{
    [RelayCommand]
    private async Task RemoveSelectedVaultAsync()
    {
        Error = "";

        if (SelectedVault is null)
        {
            Error = T(_localization, "Welcome.Status.SelectVaultFirst");
            return;
        }

        var confirmed = await _dialogs.ConfirmDangerousActionAsync(
            T(_localization, "Welcome.Remove.Title"),
            T(_localization, "Welcome.Remove.ConfirmSubtitle", SelectedVault.DisplayLabel),
            T(_localization, "Welcome.Remove.ConfirmDetail"),
            T(_localization, "Common.RemoveFromList"));

        if (!confirmed)
            return;

        try
        {
            var displayName = SelectedVault.DisplayLabel;
            var path = SelectedVault.VaultPath;

            if (!_vaultRegistry.RemoveVault(path))
            {
                Error = T(_localization, "Welcome.Error.VaultNoLongerRegistered");
                return;
            }

            if (string.Equals(_session.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _session.SetVaultPath(null);

            ReloadVaults();
            Status = T(_localization, "Welcome.Remove.StatusRemoved", displayName);
            _activity.Log("vault", "Vault removed from launcher", $"Removed {displayName} from the local vault list.", "warning", path, displayName);
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
            Error = T(_localization, "Welcome.Status.SelectVaultFirst");
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
                Error = T(_localization, "Welcome.Error.VaultNoLongerRegistered");
                return;
            }

            if (string.Equals(_session.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _session.SetVaultPath(null);

            IsRemoveOverlayOpen = false;
            RemoveTarget = null;
            ReloadVaults();
            Status = T(_localization, "Welcome.Remove.StatusRemoved", displayName);
            _activity.Log("vault", "Vault removed from launcher", $"Removed {displayName} from the local vault list.", "warning", path, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
