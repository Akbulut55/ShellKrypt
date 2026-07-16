using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Backups;
using ShellKrypt.Application.Authenticator;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Items;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Application.Settings;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Core.Backups;
using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Core.ProjectSecrets;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Desktop.Features.Authenticator;
using ShellKrypt.Desktop.Features.BackupCenter;
using ShellKrypt.Desktop.Bootstrap;
using ShellKrypt.Desktop.Services.QuickFill;
using ShellKrypt.Desktop.Services.Runtime;
using AppState = ShellKrypt.Desktop.Services.AppState;
using ClipboardService = ShellKrypt.Desktop.Services.ClipboardService;
using SessionSecurityService = ShellKrypt.Desktop.Services.SessionSecurityService;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IVaultSessionController _vaultSession;
    private readonly IDesktopSettingsController _settings;
    private readonly IDesktopDialogService _dialogs;
    private readonly ISecureClipboardService _secureClipboard;
    private readonly IActivityRecorder _activityRecorder;
    private readonly IAutomaticBackupController _automaticBackups;
    private readonly IQuickFillController _quickFillController;
    private readonly VaultRegistryService _vaultRegistryService;
    private readonly LocalizationService _localization;
    private readonly SessionSecurityService _sessionSecurity;
    private readonly IVaultService _vaultService;
    private readonly IEncryptedVaultBackupService _encryptedBackupService;
    private readonly IVaultPlaintextExportService _plaintextExportService;
    private readonly IVaultCsvImportService _csvImportService;
    private readonly DesktopNavigationService _navigation;
    private readonly QuickFillPopupFactory _quickFillPopup;
    public DesktopFeatureServices DesktopFeatures { get; }
    public IDesktopNavigation Navigation => _navigation;
    public VaultRegistryService VaultRegistry => _vaultRegistryService;
    public IVaultService VaultService => _vaultService;
    public event EventHandler? ActivityChanged;
    public event EventHandler? AutomaticBackupChanged;

    [ObservableProperty]
    private ViewModelBase current = null!;

    internal MainWindowViewModel(
        DesktopServiceCatalog services,
        DesktopNavigationService navigation,
        QuickFillPopupFactory quickFillPopup)
    {
        _vaultSession = services.VaultSession;
        _settings = services.Settings;
        _dialogs = services.Dialogs;
        _secureClipboard = services.SecureClipboard;
        _activityRecorder = services.ActivityRecorder;
        _automaticBackups = services.AutomaticBackups;
        _quickFillController = services.QuickFill;
        _vaultRegistryService = services.VaultRegistryService;
        _localization = services.Localization;
        _sessionSecurity = services.SessionSecurity;
        _vaultService = services.VaultService;
        _encryptedBackupService = services.EncryptedBackupService;
        _plaintextExportService = services.PlaintextExportService;
        _csvImportService = services.CsvImportService;
        _navigation = navigation;
        _quickFillPopup = quickFillPopup;
        DesktopFeatures = services.DesktopFeatures;

        _settings.Changed += (_, _) => NotifySettingsChanged();
        _activityRecorder.Changed += (_, _) => ActivityChanged?.Invoke(this, EventArgs.Empty);
        _automaticBackups.Changed += (_, _) => AutomaticBackupChanged?.Invoke(this, EventArgs.Empty);
        _localization.LanguageChanged += (_, _) => Current?.RefreshLocalization();
        _quickFillController.HotkeyPressed += (_, _) => OpenQuickFillPopup();
        _quickFillController.StatusChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(QuickFillHotkeyStatus));
            OnPropertyChanged(nameof(CanConfigureQuickFillSystemShortcut));
            if (Current is ShellViewModel shell)
                shell.QuickFill.RefreshHotkeyStatus();
        };

        _navigation.CurrentChanged += (_, _) => Current = _navigation.Current;
        Current = _navigation.Current;
    }

    public string? VaultPath => _vaultSession.VaultPath;
    public byte[] VaultKey => _vaultSession.VaultKey;
    public LocalizationService Localization => _localization;
    public IEncryptedVaultBackupService EncryptedBackupService => _encryptedBackupService;
    public IVaultPlaintextExportService PlaintextExportService => _plaintextExportService;
    public IVaultCsvImportService CsvImportService => _csvImportService;
    public BackupCenterHistory BackupCenterHistory => _automaticBackups.History;
    public EmergencyKitState EmergencyKit => _settings.EmergencyKit;
    public BackupScheduleSettings BackupSchedule => _automaticBackups.Schedule;
    public AutomaticBackupState AutomaticBackupState => _automaticBackups.State;
    public QuickFillSettings QuickFill => _quickFillController.Settings;
    public string QuickFillHotkeyStatus => _quickFillController.HotkeyStatus;
    public bool CanConfigureQuickFillSystemShortcut => _quickFillController.CanConfigureSystemShortcut;
    public IAutomaticBackupController AutomaticBackups => _automaticBackups;
    public bool IsUnlocked => _vaultSession.IsUnlocked;
    public string VaultPathDisplay => VaultPath ?? _localization.Get("Common.NoVaultSelected");
    public bool HasAcceptedSecurityAcknowledgement => _settings.HasAcceptedSecurityAcknowledgement;

    public bool AutoLockEnabled { get => _settings.AutoLockEnabled; set => _settings.AutoLockEnabled = value; }
    public int AutoLockMinutes { get => _settings.AutoLockMinutes; set => _settings.AutoLockMinutes = value; }
    public bool LockOnDeactivate { get => _settings.LockOnDeactivate; set => _settings.LockOnDeactivate = value; }
    public int LockOnDeactivateSeconds { get => _settings.LockOnDeactivateSeconds; set => _settings.LockOnDeactivateSeconds = value; }
    public int ClipboardClearSeconds { get => _settings.ClipboardClearSeconds; set => _settings.ClipboardClearSeconds = value; }
    public bool ClipboardCopyEnabled { get => _settings.ClipboardCopyEnabled; set => _settings.ClipboardCopyEnabled = value; }
    public bool CloseToTrayEnabled { get => _settings.CloseToTrayEnabled; set => _settings.CloseToTrayEnabled = value; }
    public int MarkdownAutoSaveSeconds { get => _settings.MarkdownAutoSaveSeconds; set => _settings.MarkdownAutoSaveSeconds = value; }
    public string ThemeId { get => _settings.ThemeId; set => _settings.ThemeId = value; }
    public string LanguageId { get => _settings.LanguageId; set => _settings.LanguageId = value; }

    private void NotifySettingsChanged()
    {
        OnPropertyChanged(nameof(AutoLockEnabled));
        OnPropertyChanged(nameof(AutoLockMinutes));
        OnPropertyChanged(nameof(LockOnDeactivate));
        OnPropertyChanged(nameof(LockOnDeactivateSeconds));
        OnPropertyChanged(nameof(ClipboardClearSeconds));
        OnPropertyChanged(nameof(ClipboardCopyEnabled));
        OnPropertyChanged(nameof(CloseToTrayEnabled));
        OnPropertyChanged(nameof(MarkdownAutoSaveSeconds));
        OnPropertyChanged(nameof(ThemeId));
        OnPropertyChanged(nameof(LanguageId));
        OnPropertyChanged(nameof(HasAcceptedSecurityAcknowledgement));
    }
}
