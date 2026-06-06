using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
        new("en", "English")
    ];

    public ObservableCollection<AutoLockDurationOption> AutoLockDurations { get; } =
    [
        new(1, "1 Minute"),
        new(5, "5 Minutes"),
        new(10, "10 Minutes"),
        new(15, "15 Minutes"),
        new(30, "30 Minutes"),
        new(60, "1 Hour"),
        new(120, "2 Hours"),
    ];

    public ObservableCollection<SecondsDurationOption> FocusLossLockDelayOptions { get; } =
    [
        new(0, "Off"),
        new(5, "5 Seconds"),
        new(10, "10 Seconds"),
        new(20, "20 Seconds"),
        new(30, "30 Seconds"),
        new(60, "1 Minute"),
        new(120, "2 Minutes"),
    ];

    public ObservableCollection<SecondsDurationOption> ClipboardClearTimeoutOptions { get; } =
    [
        new(5, "5 Seconds"),
        new(15, "15 Seconds"),
        new(30, "30 Seconds"),
        new(60, "1 Minute"),
        new(120, "2 Minutes"),
        new(300, "5 Minutes"),
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
        LoadFromRootSettings();
        SelectedLanguageOption = LanguageOptions[0];
        Status = "Settings save automatically.";

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
    public string SelectedAutoLockDurationLabel => SelectedAutoLockDuration?.Label ?? "5 Minutes";
    public string SelectedFocusLossLockDelayLabel => SelectedFocusLossLockDelay?.Label ?? "Off";
    public string SelectedClipboardClearDurationLabel => SelectedClipboardClearDuration?.Label ?? "1 Minute";
    public string ThemeModeLabel => SelectedThemeOption?.Label ?? ShellKryptThemePalettes.Default.DisplayName;
    public string SelectedLanguageLabel => SelectedLanguageOption?.Label ?? "English";
    public bool IsEnglishLanguageSelected => SelectedLanguageOption?.Code == "en";
    public string FocusLockSummary => LockOnDeactivate
        ? $"ShellKrypt locks after {SelectedFocusLossLockDelay?.Label?.ToLowerInvariant() ?? "the selected delay"} when the app loses focus."
        : "ShellKrypt stays unlocked when the app is not focused.";
    public string ClipboardClearSummary => ClipboardCopyEnabled
        ? $"Copied secrets are cleared after {SelectedClipboardClearDuration?.Label?.ToLowerInvariant() ?? "the selected timeout"}."
        : "Copy actions are disabled. Clipboard clearing is best-effort and not a security boundary.";
    public string PasswordPolicyGuidance => VaultMasterPasswordPolicy.Guidance;
    public string RecoveryGuidanceText => "If the vault is locked and the master password is forgotten, the data cannot be recovered without a prior backup.";
    public string BackupRecommendationText => "Create an encrypted .skbx backup with a separate export passphrase before changing the master password or moving the vault.";
    public string SelectedSecurityProfileDescription => SelectedSecurityProfile?.Description ?? VaultSecurityProfiles.Default.Description;
    public string SecurityStatusText => AutoLockEnabled
        ? $"Auto-lock enabled â€¢ {SelectedAutoLockDuration?.Label ?? "Configured"}"
        : "Auto-lock disabled";
}
