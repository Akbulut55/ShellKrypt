using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly VaultRegistryStore _vaultRegistry;

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
    private void SaveSelectedMetadata()
    {
        Error = "";

        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        try
        {
            _vaultRegistry.UpsertVault(
                SelectedVault.VaultPath,
                SelectedVault.DisplayName,
                SelectedVault.Description,
                SelectedVault.AccentColor,
                SelectedVault.IconKey,
                SelectedVault.IsDefault);

            ReloadVaults(SelectedVault.VaultPath);
            Status = "Vault metadata saved.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    [RelayCommand]
    private void MakeDefault()
    {
        Error = "";

        if (SelectedVault is null)
        {
            Error = "Select a vault first.";
            return;
        }

        try
        {
            _vaultRegistry.SetDefaultVault(SelectedVault.VaultPath);
            ReloadVaults(SelectedVault.VaultPath);
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
            : $"Selected vault is missing: {value.PathDisplay}";
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
                    : $"Selected vault is missing: {SelectedVault.PathDisplay}";
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
}
