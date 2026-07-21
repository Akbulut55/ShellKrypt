using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Resources.Theming;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Features.Settings;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsRuntime _root;
    private readonly IDesktopNavigation _navigation;
    private readonly ShellViewModel _shell;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryService _vaultRegistry;

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private AutoLockDurationOption? selectedAutoLockDuration;
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private SecondsDurationOption? selectedFocusLossLockDelay;
    [ObservableProperty] private SecondsDurationOption? selectedClipboardClearDuration;
    [ObservableProperty] private SecondsDurationOption? selectedMarkdownAutoSaveDuration;
    [ObservableProperty] private bool clipboardCopyEnabled;
    [ObservableProperty] private bool closeToTrayEnabled;
    [ObservableProperty] private ThemeOption? selectedThemeOption;
    [ObservableProperty] private LanguageOption? selectedLanguageOption;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string currentMasterPassword = "";
    [ObservableProperty] private string newMasterPassword = "";
    [ObservableProperty] private string confirmNewMasterPassword = "";
    [ObservableProperty] private string masterPasswordStatus = "";
    [ObservableProperty] private VaultSecurityProfile? selectedSecurityProfile;
    [ObservableProperty] private string activeSecurityProfileLabel = "Unknown";

    public ObservableCollection<ThemeOption> ThemeOptions { get; } =
    [
        .. ShellKryptThemePalettes.All.Select(theme => new ThemeOption(theme.Id, theme.DisplayName))
    ];

    public ObservableCollection<LanguageOption> LanguageOptions { get; } =
    [
        .. LanguageRegistry.All.Select(language => new LanguageOption(language.Id, language.NativeName, language.DisplayName))
    ];

    public ObservableCollection<AutoLockDurationOption> AutoLockDurations { get; } =
    [
        new(1, "Settings.Duration.1Minute", "1 Minute"),
        new(5, "Settings.Duration.5Minutes", "5 Minutes"),
        new(10, "Settings.Duration.10Minutes", "10 Minutes"),
        new(15, "Settings.Duration.15Minutes", "15 Minutes"),
        new(30, "Settings.Duration.30Minutes", "30 Minutes"),
        new(60, "Settings.Duration.1Hour", "1 Hour"),
        new(120, "Settings.Duration.2Hours", "2 Hours"),
    ];

    public ObservableCollection<SecondsDurationOption> FocusLossLockDelayOptions { get; } =
    [
        new(0, "Settings.Duration.Off", "Off"),
        new(5, "Settings.Duration.5Seconds", "5 Seconds"),
        new(10, "Settings.Duration.10Seconds", "10 Seconds"),
        new(20, "Settings.Duration.20Seconds", "20 Seconds"),
        new(30, "Settings.Duration.30Seconds", "30 Seconds"),
        new(60, "Settings.Duration.1Minute", "1 Minute"),
        new(120, "Settings.Duration.2Minutes", "2 Minutes"),
    ];

    public ObservableCollection<SecondsDurationOption> ClipboardClearTimeoutOptions { get; } =
    [
        new(5, "Settings.Duration.5Seconds", "5 Seconds"),
        new(15, "Settings.Duration.15Seconds", "15 Seconds"),
        new(30, "Settings.Duration.30Seconds", "30 Seconds"),
        new(60, "Settings.Duration.1Minute", "1 Minute"),
        new(120, "Settings.Duration.2Minutes", "2 Minutes"),
        new(300, "Settings.Duration.5Minutes", "5 Minutes"),
    ];

    public ObservableCollection<SecondsDurationOption> MarkdownAutoSaveDurationOptions { get; } =
    [
        new(0, "Settings.Duration.Off", "Off"),
        new(3, "Settings.Duration.3Seconds", "3 Seconds"),
        new(5, "Settings.Duration.5Seconds", "5 Seconds"),
        new(15, "Settings.Duration.15Seconds", "15 Seconds"),
        new(30, "Settings.Duration.30Seconds", "30 Seconds"),
        new(60, "Settings.Duration.1Minute", "1 Minute"),
    ];

    public ObservableCollection<VaultSecurityProfile> SecurityProfiles { get; } =
    [
        .. VaultSecurityProfiles.All
    ];

    public SettingsViewModel(SettingsRuntime root, IDesktopNavigation navigation, ShellViewModel shell, VaultRegistryService vaultRegistry, IVaultService vaultService)
    {
        _root = root;
        _navigation = navigation;
        _shell = shell;
        _vaultRegistry = vaultRegistry;
        _vaultService = vaultService;
        _root.Localization.LanguageChanged += OnLocalizationChanged;
        RefreshLocalizedOptionLabels();
        LoadFromRootSettings();
        SelectedLanguageOption = ResolveLanguageOption(_root.LanguageId);
        Status = T("Settings.Status.SettingsAutoSave");

        SelectedSecurityProfile = VaultSecurityProfiles.Default;
        _ = LoadCurrentSecurityProfileAsync();
    }

    public bool HasMasterPasswordStatus => !string.IsNullOrWhiteSpace(MasterPasswordStatus);
    public string ActiveVaultDisplay => GetVaultFileName();
    public string ActiveVaultPathDisplay => string.IsNullOrWhiteSpace(_root.VaultPath) ? T("Settings.Status.NoActiveVaultPath") : _root.VaultPath;
    public string VaultStorageDisplay => GetVaultStorageDisplay();
    public double VaultStoragePercent => GetVaultStoragePercent();
    public string EncryptionDisplay => "AES-256";
    public string SelectedAutoLockDurationLabel => SelectedAutoLockDuration?.Label ?? T("Settings.Duration.5Minutes");
    public string SelectedFocusLossLockDelayLabel => SelectedFocusLossLockDelay?.Label ?? T("Settings.Duration.Off");
    public string SelectedClipboardClearDurationLabel => SelectedClipboardClearDuration?.Label ?? T("Settings.Duration.1Minute");
    public string SelectedMarkdownAutoSaveDurationLabel => SelectedMarkdownAutoSaveDuration?.Label ?? T("Settings.Duration.3Seconds");
    public string SelectedSecurityProfileLabel => SelectedSecurityProfile?.Label ?? VaultSecurityProfiles.Default.Label;
    public string ThemeModeLabel => SelectedThemeOption?.Label ?? ShellKryptThemePalettes.Default.DisplayName;
    public string SelectedLanguageLabel => SelectedLanguageOption?.Label ?? LanguageRegistry.Default.NativeName;
    public string FocusLockSummary => LockOnDeactivate
        ? T("Settings.FocusLock.Enabled", LowerLabel(SelectedFocusLossLockDelay?.Label) ?? T("Settings.FocusLock.SelectedDelay"))
        : T("Settings.FocusLock.Disabled");
    public string ClipboardClearSummary => ClipboardCopyEnabled
        ? T("Settings.Clipboard.Enabled", LowerLabel(SelectedClipboardClearDuration?.Label) ?? T("Settings.Clipboard.SelectedTimeout"))
        : T("Settings.Clipboard.Disabled");
    public string MarkdownAutoSaveSummary => SelectedMarkdownAutoSaveDuration?.Seconds == 0
        ? T("Settings.MarkdownAutoSave.Disabled")
        : T("Settings.MarkdownAutoSave.Enabled", LowerLabel(SelectedMarkdownAutoSaveDuration?.Label) ?? T("Settings.MarkdownAutoSave.SelectedDelay"));
    public string PasswordPolicyGuidance => VaultMasterPasswordPolicy.Guidance;
    public string RecoveryGuidanceText => T("Settings.RecoveryGuidance");
    public string SelectedSecurityProfileDescription => SelectedSecurityProfile?.Description ?? VaultSecurityProfiles.Default.Description;
    public string SecurityStatusText => AutoLockEnabled
        ? T("Settings.SecurityStatus.AutoLockEnabled", SelectedAutoLockDuration?.Label ?? T("Settings.Status.Configured"))
        : T("Settings.SecurityStatus.AutoLockDisabled");

    private string T(string key, params object[] args) => _root.Localization.Get(key, args);

    private static string? LowerLabel(string? label) => label?.ToLowerInvariant();
}
