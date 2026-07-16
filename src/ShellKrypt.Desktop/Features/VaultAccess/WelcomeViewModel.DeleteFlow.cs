using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.Features.VaultAccess;

public sealed partial class WelcomeViewModel
{
    [RelayCommand]
    private void DeleteVault(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = T(_localization, "Welcome.Status.SelectVaultFirst");
            return;
        }

        if (!vault.Exists)
        {
            Error = T(_localization, "Welcome.Error.SelectedVaultMissing");
            return;
        }

        DeleteTarget = vault;
        DeletePassword = "";
        DeleteOverlayError = "";
        IsDeletePasswordVisible = false;
        IsDeletePasswordStep = false;
        IsDeleteOverlayOpen = true;
    }

    [RelayCommand]
    private void CancelDeleteOverlay()
    {
        if (IsBusy)
            return;

        ClearDeleteOverlay();
    }

    [RelayCommand]
    private void ContinueDeleteOverlay()
    {
        DeleteOverlayError = "";
        DeletePassword = "";
        IsDeletePasswordVisible = false;
        IsDeletePasswordStep = true;
    }

    [RelayCommand]
    private void ToggleDeletePasswordVisibility()
    {
        IsDeletePasswordVisible = !IsDeletePasswordVisible;
    }

    [RelayCommand]
    private async Task ConfirmDeleteOverlayAsync()
    {
        Error = "";
        DeleteOverlayError = "";

        var vault = DeleteTarget;
        if (vault is null)
        {
            ClearDeleteOverlay();
            return;
        }

        if (string.IsNullOrWhiteSpace(DeletePassword))
        {
            DeleteOverlayError = T(_localization, "Welcome.Delete.EnterMasterPassword");
            return;
        }

        IsBusy = true;
        try
        {
            var deletePath = VaultFileGuard.EnsureSafeVaultDeletionTarget(vault.VaultPath, vault.VaultPath);
            var unlockResult = await _vaultService.UnlockAsync(deletePath, DeletePassword);
            if (!unlockResult.Success)
            {
                DeleteOverlayError = unlockResult.Error ?? "Wrong master password.";
                return;
            }

            if (unlockResult.VaultKey is { Length: > 0 } vaultKey)
                CryptographicOperations.ZeroMemory(vaultKey);

            SqliteConnection.ClearAllPools();

            await _clipboard.ClearAsync();
            VaultFileGuard.DeleteVaultAndKnownSidecars(deletePath, vault.VaultPath);

            if (!_vaultRegistry.RemoveVault(deletePath))
            {
            Error = T(_localization, "Welcome.Error.VaultNoLongerRegistered");
                return;
            }

            if (string.Equals(_session.VaultPath, deletePath, StringComparison.OrdinalIgnoreCase))
                _session.SetVaultPath(null);

            ClearDeleteOverlay();
            ReloadVaults();
            Status = $"{vault.DisplayLabel} was deleted permanently.";
            _activity.Log("vault", "Vault deleted", $"Permanently deleted {vault.DisplayLabel}.", "danger", vault.VaultPath, vault.DisplayLabel);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearDeleteOverlay()
    {
        IsDeleteOverlayOpen = false;
        IsDeletePasswordStep = false;
        IsDeletePasswordVisible = false;
        DeleteOverlayError = "";
        DeletePassword = "";
        DeleteTarget = null;
    }
}
