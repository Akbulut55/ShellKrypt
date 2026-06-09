using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;
using ShellKrypt.UI.Shared.Theming;

namespace ShellKrypt.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly ShellViewModel _shell;
    private readonly IVaultTransferService _transferService = new SqliteVaultTransferService();
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly VaultRegistryService _vaultRegistry;

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private AutoLockDurationOption? selectedAutoLockDuration;
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private SecondsDurationOption? selectedFocusLossLockDelay;
    [ObservableProperty] private SecondsDurationOption? selectedClipboardClearDuration;
    [ObservableProperty] private bool clipboardCopyEnabled;
    [ObservableProperty] private bool isAutoLockPickerOpen;
    [ObservableProperty] private bool isFocusLockPickerOpen;
    [ObservableProperty] private bool isClipboardClearPickerOpen;
    [ObservableProperty] private ThemeOption? selectedThemeOption;
    [ObservableProperty] private LanguageOption? selectedLanguageOption;
    [ObservableProperty] private bool isThemePickerOpen;
    [ObservableProperty] private bool isLanguagePickerOpen;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private string transferStatus = "";
    [ObservableProperty] private bool isTransferBusy;
    [ObservableProperty] private string currentMasterPassword = "";
    [ObservableProperty] private string newMasterPassword = "";
    [ObservableProperty] private string confirmNewMasterPassword = "";
    [ObservableProperty] private string masterPasswordStatus = "";
    [ObservableProperty] private VaultSecurityProfile? selectedSecurityProfile;
    [ObservableProperty] private string activeSecurityProfileLabel = "Unknown";

    [ObservableProperty] private string encryptedExportPath = "";
    [ObservableProperty] private string exportPassphrase = "";
    [ObservableProperty] private string exportSummary = "";

    [ObservableProperty] private string plaintextExportPath = "";
    [ObservableProperty] private bool confirmPlaintextExport;
    [ObservableProperty] private string plaintextExportConfirmationText = "";

    [ObservableProperty] private string encryptedImportPath = "";
    [ObservableProperty] private string encryptedImportPassphrase = "";
    [ObservableProperty] private string encryptedImportSummary = "";

    [ObservableProperty] private string csvImportPath = "";
    [ObservableProperty] private VaultCsvDuplicateStrategy selectedCsvDuplicateStrategy = VaultCsvDuplicateStrategy.SkipDuplicates;
    [ObservableProperty] private string csvPreviewSummary = "";

    public ObservableCollection<VaultCsvDuplicateStrategy> CsvDuplicateStrategies { get; } =
    [
        VaultCsvDuplicateStrategy.SkipDuplicates,
        VaultCsvDuplicateStrategy.OverwriteDuplicates,
        VaultCsvDuplicateStrategy.ImportAll
    ];

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

    public ObservableCollection<VaultSecurityProfile> SecurityProfiles { get; } =
    [
        .. VaultSecurityProfiles.All
    ];

    public ObservableCollection<VaultCsvImportRowPreview> CsvPreviewRows { get; } = new();

    public SettingsViewModel(MainWindowViewModel root, ShellViewModel shell, VaultRegistryService vaultRegistry)
    {
        _root = root;
        _shell = shell;
        _vaultRegistry = vaultRegistry;
        CsvPreviewRows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCsvPreview));
        _root.Localization.LanguageChanged += OnLocalizationChanged;
        RefreshLocalizedOptionLabels();
        LoadFromRootSettings();
        SelectedLanguageOption = ResolveLanguageOption(_root.LanguageId);
        Status = T("Settings.Status.SettingsAutoSave");

        var exportBaseName = GetVaultDisplayName();
        EncryptedExportPath = DefaultPaths.GetSuggestedExportPath($"{exportBaseName} Backup", ".skbx");
        PlaintextExportPath = DefaultPaths.GetSuggestedExportPath($"{exportBaseName} DECRYPTED Plaintext Export", ".json");
        SelectedSecurityProfile = VaultSecurityProfiles.Default;
        _ = LoadCurrentSecurityProfileAsync();
    }

    public bool HasCsvPreview => CsvPreviewRows.Count > 0;
    public bool HasMasterPasswordStatus => !string.IsNullOrWhiteSpace(MasterPasswordStatus);
    public string ActiveVaultDisplay => GetVaultFileName();
    public string ActiveVaultPathDisplay => string.IsNullOrWhiteSpace(_root.VaultPath) ? "No active vault path." : _root.VaultPath;
    public string VaultStorageDisplay => GetVaultStorageDisplay();
    public double VaultStoragePercent => GetVaultStoragePercent();
    public string EncryptionDisplay => "AES-256";
    public string SelectedAutoLockDurationLabel => SelectedAutoLockDuration?.Label ?? T("Settings.Duration.5Minutes");
    public string SelectedFocusLossLockDelayLabel => SelectedFocusLossLockDelay?.Label ?? T("Settings.Duration.Off");
    public string SelectedClipboardClearDurationLabel => SelectedClipboardClearDuration?.Label ?? T("Settings.Duration.1Minute");
    public string ThemeModeLabel => SelectedThemeOption?.Label ?? ShellKryptThemePalettes.Default.DisplayName;
    public string SelectedLanguageLabel => SelectedLanguageOption?.Label ?? LanguageRegistry.Default.NativeName;
    public string FocusLockSummary => LockOnDeactivate
        ? T("Settings.FocusLock.Enabled", LowerLabel(SelectedFocusLossLockDelay?.Label) ?? "the selected delay")
        : T("Settings.FocusLock.Disabled");
    public string ClipboardClearSummary => ClipboardCopyEnabled
        ? T("Settings.Clipboard.Enabled", LowerLabel(SelectedClipboardClearDuration?.Label) ?? "the selected timeout")
        : T("Settings.Clipboard.Disabled");
    public string PasswordPolicyGuidance => VaultMasterPasswordPolicy.Guidance;
    public string RecoveryGuidanceText => T("Settings.RecoveryGuidance");
    public string BackupRecommendationText => T("Settings.BackupRecommendation");
    public string SelectedSecurityProfileDescription => SelectedSecurityProfile?.Description ?? VaultSecurityProfiles.Default.Description;
    public string SecurityStatusText => AutoLockEnabled
        ? T("Settings.SecurityStatus.AutoLockEnabled", SelectedAutoLockDuration?.Label ?? "Configured")
        : T("Settings.SecurityStatus.AutoLockDisabled");

    private string T(string key, params object[] args) => _root.Localization.Get(key, args);

    private static string? LowerLabel(string? label) => label?.ToLowerInvariant();
}
