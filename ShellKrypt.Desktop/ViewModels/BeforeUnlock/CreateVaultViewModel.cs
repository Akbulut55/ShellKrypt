using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Services;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CreateVaultViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryService _vaultRegistry;

    [ObservableProperty] private string displayName = "MyVault";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string vaultPath = DefaultPaths.GetSuggestedVaultPath("MyVault");
    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private string confirmPassword = "";
    [ObservableProperty] private VaultSecurityProfile? selectedSecurityProfile;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<VaultSecurityProfile> SecurityProfiles { get; } =
    [
        .. VaultSecurityProfiles.All
    ];

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string PasswordGuidanceText => T(_root, "CreateVault.PasswordGuidance");
    public string SelectedSecurityDescription => SelectedSecurityProfile?.Description ?? VaultSecurityProfiles.Default.Description;

    private bool _hasCustomVaultPath;
    private bool _isUpdatingSuggestedPath;

    public CreateVaultViewModel(MainWindowViewModel root, IVaultService vaultService, VaultRegistryService vaultRegistry)
    {
        _root = root;
        _vaultService = vaultService;
        _vaultRegistry = vaultRegistry;
        SelectedSecurityProfile = VaultSecurityProfiles.Default;
        UpdateSuggestedPath();
    }

    partial void OnDisplayNameChanged(string value) => UpdateSuggestedPath();
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnSelectedSecurityProfileChanged(VaultSecurityProfile? value) => OnPropertyChanged(nameof(SelectedSecurityDescription));

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
            Error = T(_root, "CreateVault.Error.MasterPasswordRequired");
            return;
        }

        var validation = VaultMasterPasswordPolicy.Validate(MasterPassword);
        if (!validation.IsValid)
        {
            Error = validation.Message;
            return;
        }

        if (MasterPassword != ConfirmPassword)
        {
            Error = T(_root, "CreateVault.Error.PasswordsMismatch");
            return;
        }

        IsBusy = true;
        try
        {
            await _vaultService.CreateAsync(VaultPath, MasterPassword, SelectedSecurityProfile?.Kdf);
            _vaultRegistry.UpsertVault(
                VaultPath,
                DisplayName,
                Description,
                isDefault: !_vaultRegistry.ListVaults().Any(),
                markOpened: true);

            _root.LogActivity("vault", "Vault created", $"Created {DisplayName.Trim()} as {Path.GetFileName(VaultPath)}.", "success", VaultPath, DisplayName.Trim());
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
            T(_root, "CreateVault.Picker.Title"),
            Path.GetFileNameWithoutExtension(suggestedPath),
            ".skvault",
            [".skvault"],
            T(_root, "CreateVault.Picker.FileType"));

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

    public override void RefreshLocalization()
    {
        NotifyLocalized(
            nameof(PasswordGuidanceText),
            nameof(SelectedSecurityDescription));
    }
}
