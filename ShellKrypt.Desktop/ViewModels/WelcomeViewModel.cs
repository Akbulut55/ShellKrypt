using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly VaultRegistryStore _vaultRegistry;
    private readonly IVaultService _vaultService = new SqliteVaultService();

    public ObservableCollection<VaultRecordVm> Vaults { get; } = new();
    public ObservableCollection<VaultRecordVm> RecentVaults { get; } = new();

    [ObservableProperty] private VaultRecordVm? selectedVault;
    [ObservableProperty] private string status = "Select a vault to unlock, or create a new one.";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public WelcomeViewModel(MainWindowViewModel root, VaultRegistryStore vaultRegistry)
    {
        _root = root;
        _vaultRegistry = vaultRegistry;
        ReloadVaults();
    }

    [RelayCommand]
    private void CreateVault() => _root.GoCreateVault();

    [RelayCommand]
    private void Refresh() => ReloadVaults(SelectedVault?.VaultPath);

    [RelayCommand]
    private void OpenSelectedVault()
    {
        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        OpenVault(SelectedVault);
    }

    [RelayCommand]
    private void OpenDefaultVault()
    {
        var defaultVault = _vaultRegistry.GetDefaultVault();
        if (defaultVault is null)
        {
            Error = "No default vault has been set yet.";
            return;
        }

        OpenVault(new VaultRecordVm(defaultVault));
    }

    [RelayCommand]
    private async Task ImportVaultAsync()
    {
        Error = "";

        try
        {
            var (confirmed, path, displayNameInput) = await _root.ShowImportVaultDialogAsync();
            if (!confirmed)
                return;

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
            var targetPath = DefaultPaths.GetSuggestedVaultPath($"{SelectedVault.DisplayLabel} Copy");
            File.Copy(SelectedVault.VaultPath, targetPath, overwrite: false);
            CopySidecarIfExists(SelectedVault.VaultPath, targetPath, "-wal");
            CopySidecarIfExists(SelectedVault.VaultPath, targetPath, "-shm");

            _vaultRegistry.UpsertVault(
                targetPath,
                $"{SelectedVault.DisplayLabel} Copy",
                SelectedVault.Description,
                isDefault: false,
                markOpened: false);

            ReloadVaults(targetPath);
            Status = "Vault duplicated.";
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
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenVault(VaultRecordVm? vault)
    {
        if (vault is null)
            return;

        Error = "";

        if (!vault.Exists)
        {
            Status = $"Vault file is missing:\n{vault.VaultPath}";
            Error = "The selected vault file could not be found.";
            ReloadVaults(vault.VaultPath);
            return;
        }

        _root.SetVaultPath(vault.VaultPath);
        _root.GoUnlock();
    }

    [RelayCommand]
    private async Task DeleteVaultAsync(VaultRecordVm? vault)
    {
        Error = "";

        if (vault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        if (!vault.Exists)
        {
            Error = "The selected vault file could not be found.";
            return;
        }

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Delete Vault",
            $"Delete {vault.DisplayLabel} permanently?",
            "This removes the vault file from disk and cannot be undone.",
            "Yes, delete it");

        if (!confirmed)
            return;

        var password = await _root.PromptPasswordAsync(
            "Confirm Master Password",
            "Enter the master password to permanently delete this vault.",
            vault.VaultPath,
            "Delete Vault");

        if (password is null)
            return;

        IsBusy = true;
        try
        {
            var unlockResult = await _vaultService.UnlockAsync(vault.VaultPath, password);
            if (!unlockResult.Success)
            {
                Error = unlockResult.Error ?? "Wrong master password.";
                return;
            }

            if (unlockResult.VaultKey is { Length: > 0 } vaultKey)
                Array.Clear(vaultKey, 0, vaultKey.Length);

            SqliteConnection.ClearAllPools();

            DeleteSidecarIfExists(vault.VaultPath, "-wal");
            DeleteSidecarIfExists(vault.VaultPath, "-shm");
            DeleteSidecarIfExists(vault.VaultPath, "-journal");
            File.Delete(vault.VaultPath);

            if (!_vaultRegistry.RemoveVault(vault.VaultPath))
            {
                Error = "That vault is no longer registered.";
                return;
            }

            if (string.Equals(_root.VaultPath, vault.VaultPath, StringComparison.OrdinalIgnoreCase))
                _root.SetVaultPath("");

            ReloadVaults();
            Status = $"{vault.DisplayLabel} was deleted permanently.";
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
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    partial void OnSelectedVaultChanged(VaultRecordVm? value)
    {
        if (value is null)
        {
            Status = Vaults.Count == 0
                ? "No vaults are registered yet. Create your first vault to continue."
                : "Select a vault to unlock.";
            return;
        }

        Status = value.Exists
            ? $"Selected {value.DisplayLabel}."
            : "Selected vault file is missing.";
    }

    private void ReloadVaults(string? selectPath = null)
    {
        IsBusy = true;
        Error = "";

        try
        {
            var registry = _vaultRegistry.Load();
            var selectedPath = NormalizePath(selectPath ?? _root.VaultPath);

            var vaults = registry.Vaults.Select(x => new VaultRecordVm(x)).ToArray();

            Vaults.Clear();
            foreach (var vault in vaults)
                Vaults.Add(vault);

            RecentVaults.Clear();
            foreach (var vault in _vaultRegistry.ListRecentVaults())
                RecentVaults.Add(new VaultRecordVm(vault));

            SelectedVault = vaults.FirstOrDefault(x => string.Equals(NormalizePath(x.VaultPath), selectedPath, StringComparison.OrdinalIgnoreCase))
                ?? vaults.FirstOrDefault(x => x.IsDefault)
                ?? vaults.FirstOrDefault();

            if (vaults.Length == 0)
            {
                Status = "No vaults are registered yet. Create your first vault to continue.";
            }
            else if (SelectedVault is not null)
            {
                Status = SelectedVault.Exists
                    ? $"Loaded {vaults.Length} vault{(vaults.Length == 1 ? "" : "s")}."
                    : "Selected vault file is missing.";
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Status = "Could not load the vault list.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "" : System.IO.Path.GetFullPath(path);

    private static void CopySidecarIfExists(string sourcePath, string targetPath, string suffix)
    {
        var source = sourcePath + suffix;
        if (!File.Exists(source))
            return;

        File.Copy(source, targetPath + suffix, overwrite: false);
    }

    private static void DeleteSidecarIfExists(string vaultPath, string suffix)
    {
        var sidecar = vaultPath + suffix;
        if (File.Exists(sidecar))
            File.Delete(sidecar);
    }
}
