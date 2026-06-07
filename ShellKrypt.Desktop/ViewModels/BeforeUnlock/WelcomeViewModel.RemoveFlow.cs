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
            Error = "Select a vault first.";
            return;
        }

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Remove Vault From List",
            $"Remove {SelectedVault.DisplayLabel} from the local vault list?",
            "This only removes the vault from ShellKrypt's local manager. The vault file stays on disk and can be added again later.",
            "Remove From List");

        if (!confirmed)
            return;

        try
        {
            var displayName = SelectedVault.DisplayLabel;
            var path = SelectedVault.VaultPath;

            if (!_vaultRegistry.RemoveVault(path))
            {
                Error = "That vault is no longer registered.";
                return;
            }

            if (string.Equals(_root.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            ReloadVaults();
            Status = $"{displayName} was removed from the local vault list.";
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
            Error = "Select a vault first.";
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
                Error = "That vault is no longer registered.";
                return;
            }

            if (string.Equals(_root.VaultPath, path, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            IsRemoveOverlayOpen = false;
            RemoveTarget = null;
            ReloadVaults();
            Status = $"{displayName} was removed from the local vault list.";
            _root.LogActivity("vault", "Vault removed from launcher", $"Removed {displayName} from the local vault list.", "warning", path, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
