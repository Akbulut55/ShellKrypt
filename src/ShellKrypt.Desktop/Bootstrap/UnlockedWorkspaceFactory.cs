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
        var allItems = new AllItemsViewModel(services.DesktopFeatures, shell, services.VaultItemSummaryService);
        var webLogins = new WebLoginsViewModel(services.DesktopFeatures, services.WebLoginService, services.PasswordGenerator, allItems.RefreshAfterMutationAsync);
        var notes = new MarkdownNotesViewModel(services.DesktopFeatures, services.NoteService, allItems.RefreshAfterMutationAsync);
        var cards = new CardsViewModel(services.DesktopFeatures, services.CardService, allItems.RefreshAfterMutationAsync);
        var authenticator = new AuthenticatorViewModel(
            services.DesktopFeatures,
            services.AuthenticatorEntryService,
            services.OneTimePasswordGenerator,
            services.AuthenticatorQrImportService,
            new AuthenticatorRefreshTimer(),
            allItems.RefreshAfterMutationAsync);
        var projectSecrets = new ProjectSecretsViewModel(
            services.DesktopFeatures,
            services.ProjectSecretService,
            services.ApiKeyService,
            services.ProjectSecretEnvParser,
            services.ProjectSecretEnvWriter,
            services.ProjectSecretScanner,
            services.ProjectSecretValueResolver,
            allItems.RefreshAfterMutationAsync);
        var apiKeys = new ApiKeysViewModel(
            services.DesktopFeatures,
            services.ApiKeyService,
            allItems.RefreshAfterMutationAsync,
            projectSecrets.RefreshApiKeysAsync);
        var cryptoTools = new CryptoToolsViewModel(
            services.DesktopFeatures,
            services.PasswordGenerator,
            services.PasswordStrengthService,
            services.HashService,
            services.Base64Service);
        var quickFill = new QuickFillViewModel(
            services.DesktopFeatures,
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
            new HealthViewModel(services.DesktopFeatures, shell, services.HealthAuditService),
            new BackupCenterViewModel(services.DesktopFeatures, services.AutomaticBackups, services.EncryptedBackupService, services.PlaintextExportService, services.CsvImportService, root),
            new SettingsViewModel(services.DesktopFeatures, root, shell, services.VaultRegistryService, services.VaultService),
            new ActivityViewModel(services.DesktopFeatures, services.ActivityRecorder.Store));
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
