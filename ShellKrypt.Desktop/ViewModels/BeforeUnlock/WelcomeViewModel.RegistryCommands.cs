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
}
