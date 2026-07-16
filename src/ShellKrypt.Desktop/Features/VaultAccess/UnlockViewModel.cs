using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using System;
using System.Globalization;
using System.Threading.Tasks;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.VaultAccess;

public partial class UnlockViewModel : ViewModelBase
{
    private readonly IVaultSessionController _session;
    private readonly IDesktopNavigation _navigation;
    private readonly LocalizationService _localization;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryService _vaultRegistry;

    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private bool showPassword;
    [ObservableProperty] private bool showRecoveryInfo;
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string PasswordVisibilityLabel => ShowPassword ? T(_localization, "Common.Hide") : T(_localization, "Common.Show");
    public string VaultStatusDisplay => T(_localization, "Unlock.Status.LockedEncrypted");
    public string EncryptionDisplay => "AES-256 GCM";
    public string LastUpdatedDisplay
    {
        get
        {
            var value = _vaultRegistry.FindByPath(_session.VaultPath ?? "")?.LastOpenedAtUtc;
            if (string.IsNullOrWhiteSpace(value))
                return T(_localization, "Common.Never");

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToLocalTime().ToString("g", CultureInfo.InvariantCulture)
                : value;
        }
    }
    public string SessionIdDisplay => string.IsNullOrWhiteSpace(_session.VaultPath)
        ? T(_localization, "Unlock.SessionUnavailable")
        : T(_localization, "Unlock.SessionId", Math.Abs((_session.VaultPath ?? string.Empty).GetHashCode()).ToString("X8", CultureInfo.InvariantCulture));

    public string VaultTitle => _vaultRegistry.FindByPath(_session.VaultPath ?? "")?.DisplayName ?? T(_localization, "Shell.VaultFallback");
    public string VaultPath => _session.VaultPath ?? T(_localization, "Common.NoVaultSelected");
    public string VaultDescription
        => T(_localization, "Unlock.Description", VaultTitle);
    public string RecoveryTitle => T(_localization, "Unlock.Recovery.Title");
    public string RecoveryBody => T(_localization, "Unlock.Recovery.Body");
    public string RecoverySecondaryBody => T(_localization, "Unlock.Recovery.SecondaryBody");

    public UnlockViewModel(IVaultService vaultService, VaultRegistryService vaultRegistry, IVaultSessionController session, IDesktopNavigation navigation, LocalizationService localization)
    {
        _vaultService = vaultService;
        _vaultRegistry = vaultRegistry;
        _session = session;
        _navigation = navigation;
        _localization = localization;
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

            if (_session.VaultPath is null)
            {
                Error = T(_localization, "Unlock.Error.NoVaultSelected");
                return;
            }

            var result = await _vaultService.UnlockAsync(_session.VaultPath, MasterPassword);
            if (!result.Success)
            {
                Error = result.Error is null
                    ? T(_localization, "Unlock.Error.Failed", T(_localization, "Activity.Time.Unknown"))
                    : T(_localization, "Unlock.Error.Failed", result.Error);
                return;
            }

            _navigation.OnUnlocked(result.VaultKey!);
        }
        finally
        {
            MasterPassword = "";
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back() => _navigation.GoWelcome();

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
