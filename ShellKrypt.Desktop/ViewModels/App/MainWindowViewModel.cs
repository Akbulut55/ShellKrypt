using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Items;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Settings;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Tools;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Tools;
using ShellKrypt.Infrastructure.Vaulting;
using AppState = ShellKrypt.Desktop.Services.AppState;
using AuthenticatorQrImportService = ShellKrypt.Desktop.Services.AuthenticatorQrImportService;
using ClipboardService = ShellKrypt.Desktop.Services.ClipboardService;
using SessionSecurityService = ShellKrypt.Desktop.Services.SessionSecurityService;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppState _state = new();
    private readonly AppSettingsService _settingsService = new(new FileAppSettingsStore());
    private readonly VaultRegistryService _vaultRegistryService = new(new FileVaultRegistryStore());
    private readonly ActivityLogService _activityLogService = new(new SqliteActivityLogStore());
    private readonly LocalizationService _localization = new();
    private readonly ClipboardService _clipboardService = new();
    private readonly AuthenticatorQrImportService _authenticatorQrImportService = new();
    private readonly SessionSecurityService _sessionSecurity;
    private readonly IVaultService _vaultService = new SqliteVaultService();
    private readonly IItemRepository _itemRepo = new SqliteItemRepository();
    private readonly IVaultItemSummaryService _vaultItemSummaryService;
    private readonly IWebLoginService _webLoginService;
    private readonly ICardService _cardService;
    private readonly INoteService _noteService;
    private readonly IAuthenticatorService _authenticatorService;
    private readonly IApiKeyService _apiKeyService;
    private readonly IHealthAuditService _healthAuditService;
    private readonly ICryptoToolsService _cryptoToolsService = new CryptoToolsService();
    private readonly IVaultTransferService _vaultTransferService = new SqliteVaultTransferService();
    private readonly AutomaticBackupCoordinator _automaticBackupCoordinator;
    private string? _securityAcknowledgementAcceptedAtUtc;
    private int _securityAcknowledgementVersionAccepted;
    private BackupCenterHistory _backupCenterHistory = new();
    private EmergencyKitState _emergencyKit = new();
    private BackupScheduleSettings _backupSchedule = new();
    private AutomaticBackupState _automaticBackupState = new();

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
    [ObservableProperty] private string themeId = AppSettings.DefaultThemeId;
    [ObservableProperty] private string languageId = AppSettings.DefaultLanguageId;

    public MainWindowViewModel()
    {
        _sessionSecurity = new SessionSecurityService(Lock);
        _vaultItemSummaryService = new VaultItemSummaryService(_itemRepo, new VaultItemPayloadReader());
        _webLoginService = new WebLoginService(_itemRepo);
        _cardService = new CardService(_itemRepo);
        _noteService = new NoteService(_itemRepo);
        _authenticatorService = new AuthenticatorService(_itemRepo);
        _apiKeyService = new ApiKeyService(_itemRepo);
        _healthAuditService = new HealthAuditService(_itemRepo);

        var settings = _settingsService.Load();
        var sessionSecurity = settings.ToSessionSecuritySettings();
        autoLockEnabled = sessionSecurity.AutoLockEnabled;
        autoLockMinutes = sessionSecurity.AutoLockMinutes;
        lockOnDeactivate = sessionSecurity.LockOnDeactivate;
        lockOnDeactivateSeconds = sessionSecurity.LockOnDeactivateSeconds;
        clipboardClearSeconds = sessionSecurity.ClipboardClearSeconds;
        clipboardCopyEnabled = sessionSecurity.ClipboardCopyEnabled;
        themeId = settings.ThemeId;
        languageId = settings.LanguageId;
        _securityAcknowledgementAcceptedAtUtc = settings.SecurityAcknowledgementAcceptedAtUtc;
        _securityAcknowledgementVersionAccepted = settings.SecurityAcknowledgementVersionAccepted;
        _backupCenterHistory = settings.BackupCenterHistory;
        _emergencyKit = settings.EmergencyKit;
        _backupSchedule = settings.BackupSchedule;
        _automaticBackupState = settings.AutomaticBackupState;
        _automaticBackupCoordinator = new AutomaticBackupCoordinator(_vaultTransferService, BuildAutomaticBackupContext);
        _automaticBackupCoordinator.StateChanged += (_, _) => AutomaticBackupChanged?.Invoke(this, EventArgs.Empty);
        _automaticBackupCoordinator.RunCompleted += (_, result) =>
        {
            RecordAutomaticBackupResult(result);
            SaveBackupScheduleState();
        };

        _sessionSecurity.ApplySettings(sessionSecurity);
        _localization.SetLanguage(languageId);
        _localization.LanguageChanged += (_, _) => Current?.RefreshLocalization();

        Current = new WelcomeViewModel(this, _vaultRegistryService);
        ApplyTheme(themeId);
    }

    public string? VaultPath => _state.VaultPath;
    public byte[] VaultKey => _state.GetVaultKeyOrThrow();
    public LocalizationService Localization => _localization;
    public IVaultTransferService VaultTransferService => _vaultTransferService;
    public BackupCenterHistory BackupCenterHistory => _backupCenterHistory;
    public EmergencyKitState EmergencyKit => _emergencyKit;
    public BackupScheduleSettings BackupSchedule => _backupSchedule;
    public AutomaticBackupState AutomaticBackupState => _automaticBackupState;
    public AutomaticBackupCoordinator AutomaticBackups => _automaticBackupCoordinator;
    public bool IsUnlocked => _state.VaultKey is not null;
    public string VaultPathDisplay => VaultPath ?? _localization.Get("Common.NoVaultSelected");
    public bool HasAcceptedSecurityAcknowledgement =>
        !string.IsNullOrWhiteSpace(_securityAcknowledgementAcceptedAtUtc) &&
        _securityAcknowledgementVersionAccepted >= AppSettings.CurrentSecurityAcknowledgementVersion;
}
