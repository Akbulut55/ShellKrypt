using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Authenticator;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Items;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Application.Settings;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Desktop.Features.Authenticator;
using ShellKrypt.Desktop.Features.BackupCenter;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Desktop.Services.QuickFill;
using ShellKrypt.Desktop.Shell.Runtime;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Infrastructure.Authenticator;
using ShellKrypt.Infrastructure.Backups;
using ShellKrypt.Infrastructure.CryptoTools;
using ShellKrypt.Infrastructure.DataTransfer;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.ProjectSecrets;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.Bootstrap;

public static class DesktopBootstrap
{
    public static MainWindowViewModel CreateMainWindowViewModel()
    {
        var state = new AppState();
        var settingsService = new AppSettingsService(new FileAppSettingsStore());
        var vaultRegistryService = new VaultRegistryService(new FileVaultRegistryStore());
        var activityLogService = new ActivityLogService(new SqliteActivityLogStore());
        var localization = new LocalizationService();
        var clipboardService = new ClipboardService();
        var sessionSecurity = new SessionSecurityService();
        var vaultService = new SqliteVaultService();
        var itemRepository = new SqliteItemRepository();
        var vaultSession = new VaultSessionController(state);
        var appearance = new DesktopAppearanceService();
        var settings = new DesktopSettingsController(settingsService, localization, sessionSecurity, appearance);
        var secureClipboard = new SecureClipboardService(clipboardService, sessionSecurity);
        var dialogs = new DesktopDialogService(sessionSecurity);
        var activityRecorder = new ActivityRecorder(activityLogService, vaultSession);

        var vaultItemSummaryService = new VaultItemSummaryService(itemRepository, new VaultItemPayloadReader());
        var webLoginService = new WebLoginService(itemRepository);
        var cardService = new CardService(itemRepository);
        var noteService = new NoteService(itemRepository);
        var authenticatorEntryService = new AuthenticatorEntryService(itemRepository);
        var oneTimePasswordGenerator = new OneTimePasswordGenerator();
        var authenticatorQrImportService = new AuthenticatorQrImageImportService(
            new AuthenticatorQrImportService(new AuthenticatorQrDecoder(), new OtpAuthUriParser()));
        var apiKeyService = new ApiKeyService(itemRepository);
        var projectSecretService = new ProjectSecretService(itemRepository);
        var projectSecretEnvParser = new EnvFileParser();
        var projectSecretEnvWriter = new EnvFileWriter();
        var projectSecretScanner = new ProjectSecretFilesystemScanner();
        var projectSecretValueResolver = new ProjectSecretValueResolver();
        var quickFillEntryService = new QuickFillEntryService(itemRepository);
        var healthAuditService = new HealthAuditService(itemRepository);
        var passwordGenerator = new PasswordGenerator();
        var passwordStrengthService = new PasswordStrengthService();
        var hashService = new HashService();
        var base64Service = new Base64Service();
        var encryptedBackupService = new EncryptedVaultBackupService();
        var plaintextExportService = new VaultPlaintextExportService();
        var csvImportService = new VaultCsvImportService();
        var automaticBackupFiles = new AutomaticBackupFileStore();
        var foregroundWindowService = new ForegroundWindowService();
        var autoTypeService = new AutoTypeService();
        var globalHotkeyService = new GlobalHotkeyService();
        var automaticBackupCoordinator = new AutomaticBackupCoordinator(
            encryptedBackupService,
            automaticBackupFiles,
            () => vaultSession.IsUnlocked && !string.IsNullOrWhiteSpace(vaultSession.VaultPath)
                ? new AutomaticBackupContext(vaultSession.VaultPath, vaultSession.VaultKey, settings.BackupSchedule, settings.AutomaticBackupState)
                : null);
        var automaticBackups = new AutomaticBackupController(automaticBackupCoordinator, settings, vaultSession, activityRecorder);
        var quickFill = new QuickFillController(globalHotkeyService, foregroundWindowService, settings);
        var desktopFeatures = new DesktopFeatureServices(vaultSession, localization, activityRecorder, secureClipboard, dialogs, settings, quickFill);

        var services = new DesktopServiceCatalog(
            vaultSession,
            settings,
            dialogs,
            secureClipboard,
            activityRecorder,
            automaticBackups,
            quickFill,
            desktopFeatures,
            vaultRegistryService,
            localization,
            authenticatorQrImportService,
            sessionSecurity,
            vaultService,
            vaultItemSummaryService,
            webLoginService,
            cardService,
            noteService,
            authenticatorEntryService,
            oneTimePasswordGenerator,
            apiKeyService,
            projectSecretService,
            projectSecretEnvParser,
            projectSecretEnvWriter,
            projectSecretScanner,
            projectSecretValueResolver,
            quickFillEntryService,
            healthAuditService,
            passwordGenerator,
            passwordStrengthService,
            hashService,
            base64Service,
            encryptedBackupService,
            plaintextExportService,
            csvImportService,
            foregroundWindowService,
            autoTypeService,
            globalHotkeyService);

        var lockedSurfaces = new LockedSurfaceFactory(vaultService, vaultRegistryService, vaultSession, dialogs, secureClipboard, activityRecorder, settings, localization);
        var unlockedWorkspaces = new UnlockedWorkspaceFactory(services);
        var quickFillPopup = new QuickFillPopupFactory(services);
        var navigation = new DesktopNavigationService(
            vaultSession,
            vaultRegistryService,
            sessionSecurity,
            secureClipboard,
            activityRecorder,
            automaticBackups,
            lockedSurfaces.CreateWelcome,
            lockedSurfaces.CreateCreateVault,
            lockedSurfaces.CreateUnlock,
            unlockedWorkspaces.Create);
        sessionSecurity.LockRequested += (_, _) => navigation.Lock();
        navigation.Initialize();
        var root = new MainWindowViewModel(services, navigation, quickFillPopup);
        return root;
    }
}
