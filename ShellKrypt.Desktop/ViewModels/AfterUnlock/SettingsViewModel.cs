using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;
using ShellKrypt.Application.Settings;
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
        ? $"Auto-lock enabled • {SelectedAutoLockDuration?.Label ?? "Configured"}"
        : "Auto-lock disabled";
    partial void OnAutoLockEnabledChanged(bool value)
    {
        _root.AutoLockEnabled = value;
        OnPropertyChanged(nameof(SecurityStatusText));
    }

    partial void OnLockOnDeactivateChanged(bool value)
    {
        _root.LockOnDeactivate = value;
        OnPropertyChanged(nameof(FocusLockSummary));
    }

    partial void OnSelectedFocusLossLockDelayChanged(SecondsDurationOption? value)
    {
        if (value is null)
            return;

        LockOnDeactivate = value.Seconds > 0;
        _root.LockOnDeactivate = LockOnDeactivate;
        if (value.Seconds > 0)
            _root.LockOnDeactivateSeconds = value.Seconds;
        Status = "Settings saved.";
        MarkSelected(FocusLossLockDelayOptions, value);
        OnPropertyChanged(nameof(SelectedFocusLossLockDelayLabel));
        OnPropertyChanged(nameof(FocusLockSummary));
    }

    partial void OnSelectedAutoLockDurationChanged(AutoLockDurationOption? value)
    {
        if (value is null)
            return;

        _root.AutoLockMinutes = value.Minutes;
        _root.AutoLockEnabled = value.Minutes > 0;
        Status = "Settings saved.";
        MarkSelected(AutoLockDurations, value);
        OnPropertyChanged(nameof(SelectedAutoLockDurationLabel));
        OnPropertyChanged(nameof(SecurityStatusText));
    }

    partial void OnSelectedThemeOptionChanged(ThemeOption? value)
    {
        if (value is null)
            return;

        _root.ThemeId = value.Id;
        Status = $"Theme switched to {value.Label}.";
        MarkSelected(ThemeOptions, value);
        OnPropertyChanged(nameof(ThemeModeLabel));
    }

    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        if (value is not null)
            Status = $"Language set to {value.Label}.";

        OnPropertyChanged(nameof(SelectedLanguageLabel));
        OnPropertyChanged(nameof(IsEnglishLanguageSelected));
    }

    partial void OnIsThemePickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsThemePickerOpen));
    }

    partial void OnIsLanguagePickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsLanguagePickerOpen));
    }

    partial void OnIsAutoLockPickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsAutoLockPickerOpen));
    }

    partial void OnIsFocusLockPickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsFocusLockPickerOpen));
    }

    partial void OnIsClipboardClearPickerOpenChanged(bool value)
    {
        if (value)
            ClosePickersExcept(nameof(IsClipboardClearPickerOpen));
    }

    [RelayCommand]
    private void ToggleAutoLockPicker() => IsAutoLockPickerOpen = !IsAutoLockPickerOpen;

    [RelayCommand]
    private void SelectAutoLockDuration(AutoLockDurationOption? option)
    {
        if (option is null)
            return;

        SelectedAutoLockDuration = option;
        IsAutoLockPickerOpen = false;
    }

    [RelayCommand]
    private void ToggleFocusLockPicker() => IsFocusLockPickerOpen = !IsFocusLockPickerOpen;

    [RelayCommand]
    private void SelectFocusLossLockDelay(SecondsDurationOption? option)
    {
        if (option is null)
            return;

        SelectedFocusLossLockDelay = option;
        IsFocusLockPickerOpen = false;
    }

    [RelayCommand]
    private void ToggleClipboardClearPicker() => IsClipboardClearPickerOpen = !IsClipboardClearPickerOpen;

    [RelayCommand]
    private void SelectClipboardClearDuration(SecondsDurationOption? option)
    {
        if (option is null)
            return;

        SelectedClipboardClearDuration = option;
        IsClipboardClearPickerOpen = false;
    }

    [RelayCommand]
    private void ToggleThemePicker() => IsThemePickerOpen = !IsThemePickerOpen;

    [RelayCommand]
    private void SelectTheme(ThemeOption? option)
    {
        if (option is null)
            return;

        SelectedThemeOption = option;
        IsThemePickerOpen = false;
    }

    [RelayCommand]
    private void ToggleLanguagePicker() => IsLanguagePickerOpen = !IsLanguagePickerOpen;

    [RelayCommand]
    private void SelectEnglishLanguage()
    {
        SelectedLanguageOption = LanguageOptions.FirstOrDefault(option => option.Code == "en") ?? LanguageOptions[0];
        IsLanguagePickerOpen = false;
    }

    private void ClosePickersExcept(string openPickerName)
    {
        if (openPickerName != nameof(IsAutoLockPickerOpen))
            IsAutoLockPickerOpen = false;
        if (openPickerName != nameof(IsFocusLockPickerOpen))
            IsFocusLockPickerOpen = false;
        if (openPickerName != nameof(IsClipboardClearPickerOpen))
            IsClipboardClearPickerOpen = false;
        if (openPickerName != nameof(IsThemePickerOpen))
            IsThemePickerOpen = false;
        if (openPickerName != nameof(IsLanguagePickerOpen))
            IsLanguagePickerOpen = false;
    }

    public void ClosePickers()
    {
        IsAutoLockPickerOpen = false;
        IsFocusLockPickerOpen = false;
        IsClipboardClearPickerOpen = false;
        IsThemePickerOpen = false;
        IsLanguagePickerOpen = false;
    }

    private static void MarkSelected(ObservableCollection<AutoLockDurationOption> options, AutoLockDurationOption? selected)
    {
        foreach (var option in options)
            option.IsSelected = ReferenceEquals(option, selected);
    }

    private static void MarkSelected(ObservableCollection<SecondsDurationOption> options, SecondsDurationOption? selected)
    {
        foreach (var option in options)
            option.IsSelected = ReferenceEquals(option, selected);
    }

    private static void MarkSelected(ObservableCollection<ThemeOption> options, ThemeOption? selected)
    {
        foreach (var option in options)
            option.IsSelected = ReferenceEquals(option, selected);
    }

    partial void OnMasterPasswordStatusChanged(string value) => OnPropertyChanged(nameof(HasMasterPasswordStatus));
    partial void OnSelectedSecurityProfileChanged(VaultSecurityProfile? value) => OnPropertyChanged(nameof(SelectedSecurityProfileDescription));

    partial void OnSelectedClipboardClearDurationChanged(SecondsDurationOption? value)
    {
        if (value is null)
            return;

        _root.ClipboardClearSeconds = value.Seconds;
        Status = "Settings saved.";
        MarkSelected(ClipboardClearTimeoutOptions, value);
        OnPropertyChanged(nameof(SelectedClipboardClearDurationLabel));
        OnPropertyChanged(nameof(ClipboardClearSummary));
    }

    partial void OnClipboardCopyEnabledChanged(bool value)
    {
        _root.ClipboardCopyEnabled = value;
        Status = value ? "Clipboard copy enabled." : "Clipboard copy disabled.";
        OnPropertyChanged(nameof(ClipboardClearSummary));
    }

    [RelayCommand]
    private void SaveChanges()
    {
        Status = "Changes saved locally.";
    }

    [RelayCommand]
    private void DiscardChanges()
    {
        LoadFromRootSettings();
        Status = "Local settings reloaded.";
    }

    [RelayCommand]
    private void ViewAudit()
    {
        _shell.ShowSecurityAudit();
    }

    [RelayCommand]
    private async Task DestroyVaultAsync()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            TransferStatus = "No active vault is selected.";
            return;
        }

        var vaultPath = VaultFileGuard.EnsureSafeVaultDeletionTarget(_root.VaultPath!);
        var displayName = Path.GetFileNameWithoutExtension(vaultPath);

        var confirmed = await _root.ConfirmDangerousActionAsync(
            "Permanently Delete Vault?",
            $"Permanently delete {displayName}?",
            "Warning: this action is irreversible. All stored passwords, markdown notes, and encrypted data within this vault will be destroyed immediately.",
            "Permanently Delete");

        if (!confirmed)
            return;

        var password = await _root.PromptPasswordAsync(
            "Confirm Master Password",
            "Enter the master password to permanently delete this vault.",
            vaultPath,
            "Delete Vault");

        if (password is null)
            return;

        await RunTransferAsync(async () =>
        {
            var unlockResult = await _vaultService.UnlockAsync(vaultPath, password);
            if (!unlockResult.Success)
            {
                TransferStatus = unlockResult.Error ?? "Wrong master password.";
                return;
            }

            if (unlockResult.VaultKey is { Length: > 0 } vaultKeyBytes)
                Array.Clear(vaultKeyBytes, 0, vaultKeyBytes.Length);

            SqliteConnection.ClearAllPools();

            await _root.ClearClipboardAsync();
            DeleteSidecarIfExists(vaultPath, "-wal");
            DeleteSidecarIfExists(vaultPath, "-shm");
            DeleteSidecarIfExists(vaultPath, "-journal");
            File.Delete(vaultPath);
            _vaultRegistry.RemoveVault(vaultPath);
            _root.SetVaultPath("");
            _root.Lock();
        });
    }

    private bool TryEnsureUnlockedVault(out string vaultPath, out byte[] vaultKey)
    {
        vaultPath = "";
        vaultKey = [];

        if (!_root.IsUnlocked || string.IsNullOrWhiteSpace(_root.VaultPath))
        {
            TransferStatus = "Unlock a vault before using import or export.";
            return false;
        }

        vaultPath = _root.VaultPath;
        vaultKey = _root.VaultKey;
        return true;
    }

    private async Task RunTransferAsync(Func<Task> action)
    {
        IsTransferBusy = true;
        try
        {
            TransferStatus = "";
            await action();
        }
        catch (Exception ex)
        {
            TransferStatus = ex.Message;
        }
        finally
        {
            IsTransferBusy = false;
        }
    }

    private string GetVaultDisplayName()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return "Vault";

        return Path.GetFileNameWithoutExtension(_root.VaultPath);
    }

    private string GetVaultStorageDisplay()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath) || !File.Exists(_root.VaultPath))
            return "640 MB / 1 GB Storage used";

        var bytes = new FileInfo(_root.VaultPath).Length;
        return $"{FormatBytes(bytes)} / 1 GB Storage used";
    }

    private double GetVaultStoragePercent()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath) || !File.Exists(_root.VaultPath))
            return 64;

        const double oneGb = 1024d * 1024d * 1024d;
        var bytes = new FileInfo(_root.VaultPath).Length;
        return Math.Clamp(bytes / oneGb * 100d, 0d, 100d);
    }

    private string GetVaultFileName()
    {
        if (string.IsNullOrWhiteSpace(_root.VaultPath))
            return "Personal_Vault_v2.skryp";

        return Path.GetFileName(_root.VaultPath);
    }

    private static string FormatBytes(long bytes)
    {
        const double kilobyte = 1024d;
        const double megabyte = 1024d * 1024d;
        const double gigabyte = 1024d * 1024d * 1024d;

        if (bytes >= gigabyte)
            return $"{bytes / gigabyte:0.#} GB";

        if (bytes >= megabyte)
            return $"{bytes / megabyte:0.#} MB";

        if (bytes >= kilobyte)
            return $"{bytes / kilobyte:0.#} KB";

        return $"{bytes} B";
    }

    private static void DeleteSidecarIfExists(string vaultPath, string suffix)
    {
        var sidecar = vaultPath + suffix;
        if (File.Exists(sidecar))
            File.Delete(sidecar);
    }

}
