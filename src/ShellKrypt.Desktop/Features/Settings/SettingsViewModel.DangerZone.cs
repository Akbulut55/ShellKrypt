using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.Features.Settings;

public sealed partial class SettingsViewModel
{
    private string? _destroyVaultPath;

    [ObservableProperty] private bool isDestroyVaultModalOpen;
    [ObservableProperty] private bool isDestroyVaultPasswordStep;
    [ObservableProperty] private string destroyVaultDisplayName = "";
    [ObservableProperty] private string destroyVaultPassword = "";
    [ObservableProperty] private string destroyVaultError = "";

    public string DestroyVaultModalTitle => IsDestroyVaultPasswordStep
        ? T("Settings.DestroyVault.PasswordTitle")
        : T("Settings.DestroyVault.Title");

    public string DestroyVaultModalSubtitle => IsDestroyVaultPasswordStep
        ? T("Settings.DestroyVault.PasswordSubtitle", DestroyVaultDisplayName)
        : T("Settings.DestroyVault.Subtitle", DestroyVaultDisplayName);

    public string DestroyVaultFooterText => IsDestroyVaultPasswordStep
        ? T("Settings.DestroyVault.PasswordFooter")
        : T("Settings.DestroyVault.Footer");

    public string DestroyVaultWarningText => T("Settings.DestroyVault.Warning");
    public string DestroyVaultPasswordLabel => T("Settings.DestroyVault.MasterPassword");
    public string DestroyVaultPasswordPlaceholder => T("Settings.DestroyVault.MasterPassword.Placeholder");
    public bool IsDestroyVaultWarningStep => IsDestroyVaultModalOpen && !IsDestroyVaultPasswordStep;

    [RelayCommand]
    private void DestroyVault()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            Status = T("Settings.Status.NoActiveVault");
            return;
        }

        try
        {
            _destroyVaultPath = VaultFileGuard.EnsureSafeVaultDeletionTarget(_root.VaultPath!, _root.VaultPath);
            DestroyVaultDisplayName = Path.GetFileNameWithoutExtension(_destroyVaultPath);
            DestroyVaultPassword = "";
            DestroyVaultError = "";
            IsDestroyVaultPasswordStep = false;
            IsDestroyVaultModalOpen = true;
            RefreshDestroyVaultModalText();
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    [RelayCommand]
    private void CancelDestroyVault()
    {
        ClearDestroyVaultModal();
    }

    [RelayCommand]
    private void ContinueDestroyVault()
    {
        DestroyVaultPassword = "";
        DestroyVaultError = "";
        IsDestroyVaultPasswordStep = true;
        RefreshDestroyVaultModalText();
    }

    [RelayCommand]
    private async Task ConfirmDestroyVaultAsync()
    {
        if (string.IsNullOrWhiteSpace(_destroyVaultPath))
        {
            DestroyVaultError = T("Settings.Status.NoActiveVault");
            return;
        }

        if (string.IsNullOrWhiteSpace(DestroyVaultPassword))
        {
            DestroyVaultError = T("Settings.DestroyVault.EnterPassword");
            return;
        }

        var vaultPath = _destroyVaultPath;
        var password = DestroyVaultPassword;
        try
        {
            var unlockResult = await _vaultService.UnlockAsync(vaultPath, password);
            if (!unlockResult.Success)
            {
                DestroyVaultError = unlockResult.Error ?? T("Settings.Status.WrongMasterPassword");
                return;
            }

            if (unlockResult.VaultKey is { Length: > 0 } vaultKeyBytes)
                CryptographicOperations.ZeroMemory(vaultKeyBytes);

            SqliteConnection.ClearAllPools();

            await _root.ClearClipboardAsync();
            VaultFileGuard.DeleteVaultAndKnownSidecars(vaultPath, _root.VaultPath);
            _vaultRegistry.RemoveVault(vaultPath);
            ClearDestroyVaultModal();
            _root.SetVaultPath("");
            _navigation.Lock();
        }
        catch (Exception ex)
        {
            DestroyVaultError = ex.Message;
        }
    }

    partial void OnIsDestroyVaultModalOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDestroyVaultWarningStep));
    }

    partial void OnIsDestroyVaultPasswordStepChanged(bool value)
    {
        RefreshDestroyVaultModalText();
        OnPropertyChanged(nameof(IsDestroyVaultWarningStep));
    }

    partial void OnDestroyVaultDisplayNameChanged(string value)
    {
        RefreshDestroyVaultModalText();
    }

    private void ClearDestroyVaultModal()
    {
        _destroyVaultPath = null;
        IsDestroyVaultModalOpen = false;
        IsDestroyVaultPasswordStep = false;
        DestroyVaultDisplayName = "";
        DestroyVaultPassword = "";
        DestroyVaultError = "";
    }

    private void RefreshDestroyVaultModalText()
    {
        OnPropertyChanged(nameof(DestroyVaultModalTitle));
        OnPropertyChanged(nameof(DestroyVaultModalSubtitle));
        OnPropertyChanged(nameof(DestroyVaultFooterText));
        OnPropertyChanged(nameof(DestroyVaultWarningText));
        OnPropertyChanged(nameof(DestroyVaultPasswordLabel));
        OnPropertyChanged(nameof(DestroyVaultPasswordPlaceholder));
    }
}
