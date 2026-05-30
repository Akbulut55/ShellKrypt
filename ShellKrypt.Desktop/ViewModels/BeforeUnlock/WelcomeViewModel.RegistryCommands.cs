using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel
{
    [RelayCommand]
    private async Task ImportVaultAsync()
    {
        Error = "";

        if (RequestSecurityAcknowledgement(SecurityAcknowledgementAction.ImportVault))
            return;

        try
        {
            var (confirmed, path, displayNameInput) = await _root.ShowImportVaultDialogAsync();
            if (!confirmed)
                return;

            path = VaultFileGuard.EnsureExistingVaultFile(path);
            if (!File.Exists(path))
            {
                Error = "That vault file does not exist.";
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(displayNameInput)
                ? Path.GetFileNameWithoutExtension(path)
                : displayNameInput.Trim();

            var entry = _vaultRegistry.UpsertVault(
                path,
                displayName,
                "",
                isDefault: !_vaultRegistry.ListVaults().Any(),
                markOpened: false);

            ReloadVaults(entry.VaultPath);
            Status = "Vault imported into the local manager.";
            _root.LogActivity("vault", "Vault added to launcher", $"Imported {displayName} into the local vault list.", "success", entry.VaultPath, displayName);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void DuplicateSelectedVault()
    {
        Error = "";

        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        if (!SelectedVault.Exists)
        {
            Error = "The selected vault file could not be found.";
            return;
        }

        try
        {
            var sourcePath = VaultFileGuard.EnsureExistingVaultFile(SelectedVault.VaultPath);
            var targetPath = VaultFileGuard.EnsureVaultFilePath(DefaultPaths.GetSuggestedVaultPath($"{SelectedVault.DisplayLabel} Copy"));
            VaultFileGuard.EnsureDifferentPaths(sourcePath, targetPath, "Vault duplicate target must be different from the source vault.");
            File.Copy(sourcePath, targetPath, overwrite: false);
            CopySidecarIfExists(sourcePath, targetPath, "-wal");
            CopySidecarIfExists(sourcePath, targetPath, "-shm");

            _vaultRegistry.UpsertVault(
                targetPath,
                $"{SelectedVault.DisplayLabel} Copy",
                SelectedVault.Description,
                isDefault: false,
                markOpened: false);

            ReloadVaults(targetPath);
            Status = "Vault duplicated.";
            _root.LogActivity("vault", "Vault duplicated", $"Created a duplicate of {SelectedVault.DisplayLabel}.", "success", targetPath, $"{SelectedVault.DisplayLabel} Copy");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

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

    [RelayCommand]
    private async Task EditVaultAsync(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = "Select a vault first.";
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
            Status = "Vault metadata saved.";
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
            Error = "Select a vault first.";
            return;
        }

        try
        {
            _vaultRegistry.SetDefaultVault(vault.VaultPath);
            ReloadVaults(vault.VaultPath);
            Status = "Default vault updated.";
            _root.LogActivity("vault", "Default vault changed", $"Marked {vault.DisplayLabel} as the default vault.", "info", vault.VaultPath, vault.DisplayLabel);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
