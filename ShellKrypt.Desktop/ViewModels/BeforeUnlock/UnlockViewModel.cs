using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ShellKrypt.Desktop.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private const string LegacyDescription = "Legacy default vault";
    private const string DefaultUnlockDescription = "Unlock this local vault to continue securely.";
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryStore _vaultRegistry;

    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private bool showPassword;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string PasswordVisibilityLabel => ShowPassword ? "Hide" : "Reveal";
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
    {
        get
        {
            var description = _vaultRegistry.FindByPath(_root.VaultPath ?? "")?.Description?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(description) ||
                string.Equals(description, LegacyDescription, System.StringComparison.OrdinalIgnoreCase))
            {
                return DefaultUnlockDescription;
            }

            return description;
        }
    }

    public UnlockViewModel(MainWindowViewModel root, IVaultService vaultService, VaultRegistryStore vaultRegistry)
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
}
