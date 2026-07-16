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
            var (confirmed, path, displayNameInput) = await _dialogs.ShowImportVaultDialogAsync();
            if (!confirmed)
                return;

            path = VaultFileGuard.EnsureExistingVaultFile(path);
            if (!File.Exists(path))
            {
            Error = T(_localization, "Welcome.Error.VaultFileDoesNotExist");
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(displayNameInput)
                ? Path.GetFileNameWithoutExtension(path)
                : displayNameInput.Trim();

            var entry = _vaultRegistry.UpsertVault(
                path,
                displayName,
                "",
                markOpened: false);

            ReloadVaults(entry.VaultPath);
        Status = T(_localization, "Welcome.Status.VaultImported");
            _activity.Log("vault", "Vault added to launcher", $"Imported {displayName} into the local vault list.", "success", entry.VaultPath, displayName);
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
            Error = T(_localization, "Welcome.Status.SelectVaultFirst");
            return;
        }

        if (!SelectedVault.Exists)
        {
            Error = T(_localization, "Welcome.Error.SelectedVaultMissing");
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
                markOpened: false);

            ReloadVaults(targetPath);
        Status = T(_localization, "Welcome.Status.VaultDuplicated");
            _activity.Log("vault", "Vault duplicated", $"Created a duplicate of {SelectedVault.DisplayLabel}.", "success", targetPath, $"{SelectedVault.DisplayLabel} Copy");
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
