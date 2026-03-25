using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CreateVaultViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryStore _vaultRegistry;

    [ObservableProperty] private string displayName = "My Vault";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string vaultPath = DefaultPaths.GetSuggestedVaultPath("My Vault");
    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private string confirmPassword = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isDefaultVault;

    public CreateVaultViewModel(MainWindowViewModel root, IVaultService vaultService, VaultRegistryStore vaultRegistry)
    {
        _root = root;
        _vaultService = vaultService;
        _vaultRegistry = vaultRegistry;

        IsDefaultVault = !_vaultRegistry.ListVaults().Any();
        UpdateSuggestedPath();
    }

    partial void OnDisplayNameChanged(string value) => UpdateSuggestedPath();

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            Error = "Master password is required.";
            return;
        }

        if (MasterPassword != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            await _vaultService.CreateAsync(VaultPath, MasterPassword);
            _vaultRegistry.UpsertVault(
                VaultPath,
                DisplayName,
                Description,
                isDefault: IsDefaultVault,
                markOpened: true);

            _root.SetVaultPath(VaultPath);
            _root.GoUnlock();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            MasterPassword = "";
            ConfirmPassword = "";
        }
    }

    [RelayCommand]
    private void Back() => _root.GoWelcome();

    private void UpdateSuggestedPath()
    {
        VaultPath = DefaultPaths.GetSuggestedVaultPath(DisplayName);
    }
}
