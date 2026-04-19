using System;
using System.IO;
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

    [ObservableProperty] private string displayName = "MyVault";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string vaultPath = DefaultPaths.GetSuggestedVaultPath("MyVault");
    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private string confirmPassword = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    private bool _hasCustomVaultPath;
    private bool _isUpdatingSuggestedPath;

    public CreateVaultViewModel(MainWindowViewModel root, IVaultService vaultService, VaultRegistryStore vaultRegistry)
    {
        _root = root;
        _vaultService = vaultService;
        _vaultRegistry = vaultRegistry;
        UpdateSuggestedPath();
    }

    partial void OnDisplayNameChanged(string value) => UpdateSuggestedPath();
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnVaultPathChanged(string value)
    {
        if (_isUpdatingSuggestedPath)
            return;

        _hasCustomVaultPath = !string.Equals(value, DefaultPaths.GetSuggestedVaultPath(DisplayName), StringComparison.OrdinalIgnoreCase);
    }

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
                isDefault: !_vaultRegistry.ListVaults().Any(),
                markOpened: true);

            _root.LogActivity("vault", "Vault created", $"Created {DisplayName.Trim()} at {VaultPath}.", "success", VaultPath);
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

    [RelayCommand]
    private async Task BrowseVaultPathAsync()
    {
        Error = "";
        var suggestedPath = string.IsNullOrWhiteSpace(VaultPath) ? DefaultPaths.GetSuggestedVaultPath(DisplayName) : VaultPath;
        var selectedPath = await _root.PickSaveFileAsync(
            "Choose vault location",
            Path.GetFileNameWithoutExtension(suggestedPath),
            ".skvault",
            [".skvault"],
            "ShellKrypt Vault");

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        _hasCustomVaultPath = true;
        VaultPath = selectedPath;
    }

    private void UpdateSuggestedPath()
    {
        if (_hasCustomVaultPath)
            return;

        _isUpdatingSuggestedPath = true;
        try
        {
            VaultPath = DefaultPaths.GetSuggestedVaultPath(DisplayName);
        }
        finally
        {
            _isUpdatingSuggestedPath = false;
        }
    }
}
