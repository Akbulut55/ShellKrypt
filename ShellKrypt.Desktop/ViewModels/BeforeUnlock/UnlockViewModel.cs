using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private const string DefaultUnlockDescription = "You are opening this local encrypted vault. Enter the master password to decrypt it on this device.";
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryService _vaultRegistry;

    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private bool showPassword;
    [ObservableProperty] private bool showRecoveryInfo;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string PasswordVisibilityLabel => ShowPassword ? "Hide" : "Show";
    public string VaultStatusDisplay => "Locked & Encrypted";
    public string EncryptionDisplay => "AES-256 GCM";
    public string LastUpdatedDisplay
    {
        get
        {
            var value = _vaultRegistry.FindByPath(_root.VaultPath ?? "")?.LastOpenedAtUtc;
            if (string.IsNullOrWhiteSpace(value))
                return "Never";

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToLocalTime().ToString("g", CultureInfo.InvariantCulture)
                : value;
        }
    }
    public string SessionIdDisplay => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? "SESSION UNAVAILABLE"
        : $"SESSION {Math.Abs((_root.VaultPath ?? string.Empty).GetHashCode()):X8}";

    public string VaultTitle => _vaultRegistry.FindByPath(_root.VaultPath ?? "")?.DisplayName ?? "Unlock Vault";
    public string VaultPath => _root.VaultPath ?? "(no vault selected)";
    public string VaultDescription
        => DefaultUnlockDescription;
    public string RecoveryTitle => "Master password recovery is not available";
    public string RecoveryBody => "If this vault is still unlocked on this device, open Settings and change the master password or export an encrypted backup before locking it again.";
    public string RecoverySecondaryBody => "If the vault is already locked and no backup exists, the encrypted contents cannot be recovered by design.";

    public UnlockViewModel(MainWindowViewModel root, IVaultService vaultService, VaultRegistryService vaultRegistry)
    {
        _root = root;
        _vaultService = vaultService;
        _vaultRegistry = vaultRegistry;
    }

    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnShowPasswordChanged(bool value) => OnPropertyChanged(nameof(PasswordVisibilityLabel));

    [RelayCommand]
    private async Task UnlockAsync()
    {
        IsBusy = true;
        try
        {
            Error = "";

            if (_root.VaultPath is null)
            {
                Error = "No vault selected. Go back and choose a vault.";
                return;
            }

            var result = await _vaultService.UnlockAsync(_root.VaultPath, MasterPassword);
            if (!result.Success)
            {
                Error = result.Error ?? "Unlock failed.";
                return;
            }

            _root.OnUnlocked(result.VaultKey!);
        }
        finally
        {
            MasterPassword = "";
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back() => _root.GoWelcome();

    [RelayCommand]
    private void TogglePasswordVisibility() => ShowPassword = !ShowPassword;

    [RelayCommand]
    private void ShowRecovery() => ShowRecoveryInfo = true;

    [RelayCommand]
    private void CloseRecovery() => ShowRecoveryInfo = false;
}
