using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IVaultSessionController _vaultSession;
    private readonly IDesktopSettingsController _settings;
    private readonly ISecureClipboardService _secureClipboard;
    private readonly IAutomaticBackupController _automaticBackups;
    private readonly LocalizationService _localization;
    private readonly SessionSecurityService _sessionSecurity;
    private readonly DesktopNavigationService _navigation;
    internal IVaultSessionController Session => _vaultSession;
    internal IDesktopNavigation Navigation => _navigation;
    internal IAutomaticBackupController AutomaticBackups => _automaticBackups;

    [ObservableProperty]
    private ViewModelBase current = null!;

    internal MainWindowViewModel(
        IVaultSessionController vaultSession,
        IDesktopSettingsController settings,
        ISecureClipboardService secureClipboard,
        IAutomaticBackupController automaticBackups,
        LocalizationService localization,
        SessionSecurityService sessionSecurity,
        DesktopNavigationService navigation)
    {
        _vaultSession = vaultSession;
        _settings = settings;
        _secureClipboard = secureClipboard;
        _automaticBackups = automaticBackups;
        _localization = localization;
        _sessionSecurity = sessionSecurity;
        _navigation = navigation;
        _settings.Changed += (_, _) => NotifySettingsChanged();
        _localization.LanguageChanged += (_, _) => Current?.RefreshLocalization();
        _navigation.CurrentChanged += (_, _) => Current = _navigation.Current;
        Current = _navigation.Current;
    }

    public bool IsUnlocked => _vaultSession.IsUnlocked;
    public string ThemeId => _settings.ThemeId;
    public LocalizationService Localization => _localization;
    public bool CloseToTrayEnabled { get => _settings.CloseToTrayEnabled; set => _settings.CloseToTrayEnabled = value; }

    private void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(CloseToTrayEnabled));
    }
}
