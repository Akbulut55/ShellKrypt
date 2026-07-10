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
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryService _vaultRegistry;

    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private bool showPassword;
    [ObservableProperty] private bool showRecoveryInfo;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string PasswordVisibilityLabel => ShowPassword ? T(_root, "Common.Hide") : T(_root, "Common.Show");
    public string VaultStatusDisplay => T(_root, "Unlock.Status.LockedEncrypted");
    public string EncryptionDisplay => "AES-256 GCM";
    public string LastUpdatedDisplay
    {
        get
        {
            var value = _vaultRegistry.FindByPath(_root.VaultPath ?? "")?.LastOpenedAtUtc;
            if (string.IsNullOrWhiteSpace(value))
                return T(_root, "Common.Never");

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToLocalTime().ToString("g", CultureInfo.InvariantCulture)
                : value;
        }
    }
    public string SessionIdDisplay => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? T(_root, "Unlock.SessionUnavailable")
        : T(_root, "Unlock.SessionId", Math.Abs((_root.VaultPath ?? string.Empty).GetHashCode()).ToString("X8", CultureInfo.InvariantCulture));

    public string VaultTitle => _vaultRegistry.FindByPath(_root.VaultPath ?? "")?.DisplayName ?? T(_root, "Shell.VaultFallback");
    public string VaultPath => _root.VaultPath ?? T(_root, "Common.NoVaultSelected");
    public string VaultDescription
        => T(_root, "Unlock.Description", VaultTitle);
    public string RecoveryTitle => T(_root, "Unlock.Recovery.Title");
    public string RecoveryBody => T(_root, "Unlock.Recovery.Body");
    public string RecoverySecondaryBody => T(_root, "Unlock.Recovery.SecondaryBody");

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
                Error = T(_root, "Unlock.Error.NoVaultSelected");
                return;
            }

            var result = await _vaultService.UnlockAsync(_root.VaultPath, MasterPassword);
            if (!result.Success)
            {
                Error = result.Error is null
                    ? T(_root, "Unlock.Error.Failed", T(_root, "Activity.Time.Unknown"))
                    : T(_root, "Unlock.Error.Failed", result.Error);
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

    public override void RefreshLocalization()
    {
        NotifyLocalized(
            nameof(PasswordVisibilityLabel),
            nameof(VaultStatusDisplay),
            nameof(LastUpdatedDisplay),
            nameof(SessionIdDisplay),
            nameof(VaultTitle),
            nameof(VaultPath),
            nameof(VaultDescription),
            nameof(RecoveryTitle),
            nameof(RecoveryBody),
            nameof(RecoverySecondaryBody));
    }
}
