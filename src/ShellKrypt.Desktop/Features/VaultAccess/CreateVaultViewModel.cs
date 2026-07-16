using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.VaultAccess;

public partial class CreateVaultViewModel : ViewModelBase
{
    private readonly IVaultSessionController _session;
    private readonly IDesktopNavigation _navigation;
    private readonly IDesktopDialogService _dialogs;
    private readonly IActivityRecorder _activity;
    private readonly LocalizationService _localization;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryService _vaultRegistry;
    private readonly IDesktopFileService _files;

    [ObservableProperty] private string displayName = "MyVault";
    [ObservableProperty] private string description = "";
    [ObservableProperty] private string vaultPath = "";
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
    public string PasswordGuidanceText => T(_localization, "CreateVault.PasswordGuidance");
    public string SelectedSecurityDescription => SelectedSecurityProfile?.Description ?? VaultSecurityProfiles.Default.Description;

    private bool _hasCustomVaultPath;
    private bool _isUpdatingSuggestedPath;

    public CreateVaultViewModel(
        IVaultService vaultService,
        VaultRegistryService vaultRegistry,
        IVaultSessionController session,
        IDesktopNavigation navigation,
        IDesktopDialogService dialogs,
        IActivityRecorder activity,
        LocalizationService localization,
        IDesktopFileService files)
    {
        _vaultService = vaultService;
        _vaultRegistry = vaultRegistry;
        _session = session;
        _navigation = navigation;
        _dialogs = dialogs;
        _activity = activity;
        _localization = localization;
        _files = files;
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

        _hasCustomVaultPath = !string.Equals(value, _files.GetSuggestedVaultPath(DisplayName), StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            Error = T(_localization, "CreateVault.Error.MasterPasswordRequired");
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
            Error = T(_localization, "CreateVault.Error.PasswordsMismatch");
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
                markOpened: true);

            _activity.Log("vault", "Vault created", $"Created {DisplayName.Trim()} as {Path.GetFileName(VaultPath)}.", "success", VaultPath, DisplayName.Trim());
            _session.SetVaultPath(VaultPath);
            _navigation.GoUnlock();
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
    private void Back() => _navigation.GoWelcome();

    [RelayCommand]
    private async Task BrowseVaultPathAsync()
    {
        Error = "";
        var suggestedPath = string.IsNullOrWhiteSpace(VaultPath) ? _files.GetSuggestedVaultPath(DisplayName) : VaultPath;
        var selectedPath = await _dialogs.PickSaveFileAsync(
            T(_localization, "CreateVault.Picker.Title"),
            Path.GetFileNameWithoutExtension(suggestedPath),
            ".skvault",
            [".skvault"],
            T(_localization, "CreateVault.Picker.FileType"));

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
            VaultPath = _files.GetSuggestedVaultPath(DisplayName);
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
