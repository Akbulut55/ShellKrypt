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
using AppState = ShellKrypt.Desktop.Services.AppState;
using ClipboardService = ShellKrypt.Desktop.Services.ClipboardService;
using SessionSecurityService = ShellKrypt.Desktop.Services.SessionSecurityService;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppState _state;
    private readonly AppSettingsService _settingsService;
    private readonly VaultRegistryService _vaultRegistryService;
    private readonly ActivityLogService _activityLogService;
    private readonly LocalizationService _localization;
    private readonly ClipboardService _clipboardService;
    private readonly SessionSecurityService _sessionSecurity;
    private readonly IVaultService _vaultService;
    private readonly IEncryptedVaultBackupService _encryptedBackupService;
    private readonly IVaultPlaintextExportService _plaintextExportService;
    private readonly IVaultCsvImportService _csvImportService;
    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly GlobalHotkeyService _globalHotkeyService;
    private readonly AutomaticBackupCoordinator _automaticBackupCoordinator;
    private readonly LockedSurfaceFactory _lockedSurfaces;
    private readonly UnlockedWorkspaceFactory _unlockedWorkspaces;
    private readonly QuickFillPopupFactory _quickFillPopup;
    private string? _securityAcknowledgementAcceptedAtUtc;
    private int _securityAcknowledgementVersionAccepted;
    private BackupCenterHistory _backupCenterHistory = new();
    private EmergencyKitState _emergencyKit = new();
    private BackupScheduleSettings _backupSchedule = new();
    private AutomaticBackupState _automaticBackupState = new();
    private QuickFillSettings _quickFill = new();

    public event EventHandler? ActivityChanged;
    public event EventHandler? AutomaticBackupChanged;

    [ObservableProperty]
    private ViewModelBase current = null!;

    [ObservableProperty] private bool autoLockEnabled;
    [ObservableProperty] private int autoLockMinutes;
    [ObservableProperty] private bool lockOnDeactivate;
    [ObservableProperty] private int lockOnDeactivateSeconds;
    [ObservableProperty] private int clipboardClearSeconds;
    [ObservableProperty] private bool clipboardCopyEnabled;
    [ObservableProperty] private bool closeToTrayEnabled;
    [ObservableProperty] private int markdownAutoSaveSeconds = 3;
    [ObservableProperty] private string themeId = AppSettings.DefaultThemeId;
    [ObservableProperty] private string languageId = AppSettings.DefaultLanguageId;

    internal MainWindowViewModel(
        DesktopServiceCatalog services,
        LockedSurfaceFactory lockedSurfaces,
        UnlockedWorkspaceFactory unlockedWorkspaces,
        QuickFillPopupFactory quickFillPopup)
    {
        _state = services.State;
        _settingsService = services.SettingsService;
        _vaultRegistryService = services.VaultRegistryService;
        _activityLogService = services.ActivityLogService;
        _localization = services.Localization;
        _clipboardService = services.ClipboardService;
        _sessionSecurity = services.SessionSecurity;
        _vaultService = services.VaultService;
        _encryptedBackupService = services.EncryptedBackupService;
        _plaintextExportService = services.PlaintextExportService;
        _csvImportService = services.CsvImportService;
        _foregroundWindowService = services.ForegroundWindowService;
        _globalHotkeyService = services.GlobalHotkeyService;
        _automaticBackupCoordinator = services.AutomaticBackupCoordinator;
        _lockedSurfaces = lockedSurfaces;
        _unlockedWorkspaces = unlockedWorkspaces;
        _quickFillPopup = quickFillPopup;

        var settings = _settingsService.Load();
        var sessionSecurity = settings.ToSessionSecuritySettings();
        autoLockEnabled = sessionSecurity.AutoLockEnabled;
        autoLockMinutes = sessionSecurity.AutoLockMinutes;
        lockOnDeactivate = sessionSecurity.LockOnDeactivate;
        lockOnDeactivateSeconds = sessionSecurity.LockOnDeactivateSeconds;
        clipboardClearSeconds = sessionSecurity.ClipboardClearSeconds;
        clipboardCopyEnabled = sessionSecurity.ClipboardCopyEnabled;
        closeToTrayEnabled = settings.CloseToTrayEnabled;
        markdownAutoSaveSeconds = Math.Max(1, settings.MarkdownAutoSaveSeconds);
        themeId = settings.ThemeId;
        languageId = settings.LanguageId;
        _securityAcknowledgementAcceptedAtUtc = settings.SecurityAcknowledgementAcceptedAtUtc;
        _securityAcknowledgementVersionAccepted = settings.SecurityAcknowledgementVersionAccepted;
        _backupCenterHistory = settings.BackupCenterHistory;
        _emergencyKit = settings.EmergencyKit;
        _backupSchedule = settings.BackupSchedule;
        _automaticBackupState = settings.AutomaticBackupState;
        _quickFill = settings.QuickFill;
        _automaticBackupCoordinator.StateChanged += (_, _) => AutomaticBackupChanged?.Invoke(this, EventArgs.Empty);
        _automaticBackupCoordinator.RunCompleted += (_, result) =>
        {
            RecordAutomaticBackupResult(result);
            SaveBackupScheduleState();
        };

        _sessionSecurity.ApplySettings(sessionSecurity);
        _localization.SetLanguage(languageId);
        _localization.LanguageChanged += (_, _) => Current?.RefreshLocalization();
        _globalHotkeyService.HotkeyPressed += (_, _) => OpenQuickFillPopup();
        _globalHotkeyService.StatusChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(QuickFillHotkeyStatus));
            OnPropertyChanged(nameof(CanConfigureQuickFillSystemShortcut));
            if (Current is ShellViewModel shell)
                shell.QuickFill.RefreshHotkeyStatus();
        };

        Current = _lockedSurfaces.CreateWelcome(this);
        ApplyTheme(themeId);
    }

    public string? VaultPath => _state.VaultPath;
    public byte[] VaultKey => _state.GetVaultKeyOrThrow();
    public LocalizationService Localization => _localization;
    public IEncryptedVaultBackupService EncryptedBackupService => _encryptedBackupService;
    public IVaultPlaintextExportService PlaintextExportService => _plaintextExportService;
    public IVaultCsvImportService CsvImportService => _csvImportService;
    public BackupCenterHistory BackupCenterHistory => _backupCenterHistory;
    public EmergencyKitState EmergencyKit => _emergencyKit;
    public BackupScheduleSettings BackupSchedule => _backupSchedule;
    public AutomaticBackupState AutomaticBackupState => _automaticBackupState;
    public QuickFillSettings QuickFill => _quickFill;
    public string QuickFillHotkeyStatus => _globalHotkeyService.Status;
    public bool CanConfigureQuickFillSystemShortcut => _globalHotkeyService.CanConfigurePortalShortcut;
    public AutomaticBackupCoordinator AutomaticBackups => _automaticBackupCoordinator;
    public bool IsUnlocked => _state.VaultKey is not null;
    public string VaultPathDisplay => VaultPath ?? _localization.Get("Common.NoVaultSelected");
    public bool HasAcceptedSecurityAcknowledgement =>
        !string.IsNullOrWhiteSpace(_securityAcknowledgementAcceptedAtUtc) &&
        _securityAcknowledgementVersionAccepted >= AppSettings.CurrentSecurityAcknowledgementVersion;
}
