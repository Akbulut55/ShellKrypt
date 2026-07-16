using ShellKrypt.Desktop.Features.Authenticator;
using ShellKrypt.Desktop.Features.BackupCenter;
using ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;
using ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;
using ShellKrypt.Desktop.Features.ItemWorkspaces.WebLogins;
using ShellKrypt.Desktop.Features.ProjectSecrets;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Features.CryptoTools;
using ShellKrypt.Desktop.Features.QuickFill;
using ShellKrypt.Desktop.Shell.Runtime;

namespace ShellKrypt.Desktop.Bootstrap;

internal sealed class UnlockedWorkspaceFactory(DesktopServiceCatalog services)
{
    public ShellViewModel Create(IDesktopNavigation navigation)
        => new(services.Localization, services.VaultSession, navigation, shell => CreateWorkspaces(navigation, shell));

    private ShellWorkspaces CreateWorkspaces(IDesktopNavigation root, ShellViewModel shell)
    {
        var itemRuntime = new ItemWorkspaceRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.SecureClipboard);
        var allItems = new AllItemsViewModel(new AllItemsRuntime(services.VaultSession, services.Localization), shell, services.VaultItemSummaryService);
        var webLogins = new WebLoginsViewModel(itemRuntime, services.WebLoginService, services.PasswordGenerator, allItems.RefreshAfterMutationAsync);
        var notes = new MarkdownNotesViewModel(new MarkdownNotesRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.SecureClipboard, services.Settings), services.NoteService, allItems.RefreshAfterMutationAsync);
        var cards = new CardsViewModel(itemRuntime, services.CardService, allItems.RefreshAfterMutationAsync);
        var authenticator = new AuthenticatorViewModel(
            new AuthenticatorRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.SecureClipboard, services.Dialogs),
            services.AuthenticatorEntryService,
            services.OneTimePasswordGenerator,
            services.AuthenticatorQrImportService,
            new AuthenticatorRefreshTimer(),
            allItems.RefreshAfterMutationAsync);
        var projectSecrets = new ProjectSecretsViewModel(
            new ProjectSecretsRuntime(services.VaultSession, services.ActivityRecorder, services.SecureClipboard, services.Dialogs),
            services.ProjectSecretService,
            services.ApiKeyService,
            services.ProjectSecretEnvParser,
            services.ProjectSecretEnvWriter,
            services.ProjectSecretScanner,
            services.ProjectSecretValueResolver,
            allItems.RefreshAfterMutationAsync);
        var apiKeys = new ApiKeysViewModel(
            itemRuntime,
            services.ApiKeyService,
            allItems.RefreshAfterMutationAsync,
            projectSecrets.RefreshApiKeysAsync);
        var cryptoTools = new CryptoToolsViewModel(
            new CryptoToolsRuntime(services.Localization, services.ActivityRecorder, services.SecureClipboard),
            services.PasswordGenerator,
            services.PasswordStrengthService,
            services.HashService,
            services.Base64Service);
        var quickFill = new QuickFillViewModel(
            new QuickFillRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.SecureClipboard, services.Dialogs, services.QuickFill),
            services.QuickFillEntryService,
            services.WebLoginService,
            services.CardService,
            services.ApiKeyService,
            services.AuthenticatorEntryService);

        return new ShellWorkspaces(
            allItems,
            webLogins,
            notes,
            cards,
            authenticator,
            apiKeys,
            projectSecrets,
            cryptoTools,
            quickFill,
            new HealthViewModel(new SecurityAuditRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.Settings), shell, services.HealthAuditService),
            new BackupCenterViewModel(new BackupCenterRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.SecureClipboard, services.Dialogs, services.Files), services.AutomaticBackups, services.EncryptedBackupService, services.PlaintextExportService, services.CsvImportService, root),
            new SettingsViewModel(new SettingsRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.SecureClipboard, services.Settings, services.Files), root, shell, services.VaultRegistryService, services.VaultService),
            new ActivityViewModel(new ActivityLogsRuntime(services.VaultSession, services.Localization, services.ActivityRecorder, services.Dialogs), services.ActivityRecorder.Store));
    }
}

internal sealed record ShellWorkspaces(
    AllItemsViewModel AllItems,
    WebLoginsViewModel WebLogins,
    MarkdownNotesViewModel MarkdownNotes,
    CardsViewModel Cards,
    AuthenticatorViewModel Authenticator,
    ApiKeysViewModel ApiKeys,
    ProjectSecretsViewModel ProjectSecrets,
    CryptoToolsViewModel CryptoTools,
    QuickFillViewModel QuickFill,
    HealthViewModel Health,
    BackupCenterViewModel BackupCenter,
    SettingsViewModel Settings,
    ActivityViewModel Activity);
