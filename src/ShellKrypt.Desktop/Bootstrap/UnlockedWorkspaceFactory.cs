using ShellKrypt.Desktop.Features.Authenticator;
using ShellKrypt.Desktop.Features.BackupCenter;
using ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;
using ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;
using ShellKrypt.Desktop.Features.ItemWorkspaces.WebLogins;
using ShellKrypt.Desktop.Features.ProjectSecrets;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.ViewModels.AfterUnlock.CryptoTools;
using ShellKrypt.Desktop.ViewModels.AfterUnlock.QuickFill;

namespace ShellKrypt.Desktop.Bootstrap;

internal sealed class UnlockedWorkspaceFactory(DesktopServiceCatalog services)
{
    public ShellViewModel Create(MainWindowViewModel root)
        => new(root, shell => CreateWorkspaces(root, shell));

    private ShellWorkspaces CreateWorkspaces(MainWindowViewModel root, ShellViewModel shell)
    {
        var allItems = new AllItemsViewModel(root, shell, services.VaultItemSummaryService);
        var webLogins = new WebLoginsViewModel(root, services.WebLoginService, services.PasswordGenerator, allItems.RefreshAfterMutationAsync);
        var notes = new MarkdownNotesViewModel(root, services.NoteService, allItems.RefreshAfterMutationAsync);
        var cards = new CardsViewModel(root, services.CardService, allItems.RefreshAfterMutationAsync);
        var authenticator = new AuthenticatorViewModel(
            root,
            services.AuthenticatorEntryService,
            services.OneTimePasswordGenerator,
            services.AuthenticatorQrImportService,
            new AuthenticatorRefreshTimer(),
            allItems.RefreshAfterMutationAsync);
        var projectSecrets = new ProjectSecretsViewModel(
            root,
            services.ProjectSecretService,
            services.ApiKeyService,
            services.ProjectSecretEnvParser,
            services.ProjectSecretEnvWriter,
            services.ProjectSecretScanner,
            services.ProjectSecretValueResolver,
            allItems.RefreshAfterMutationAsync);
        var apiKeys = new ApiKeysViewModel(
            root,
            services.ApiKeyService,
            allItems.RefreshAfterMutationAsync,
            projectSecrets.RefreshApiKeysAsync);
        var cryptoTools = new CryptoToolsViewModel(
            root,
            services.PasswordGenerator,
            services.PasswordStrengthService,
            services.HashService,
            services.Base64Service);
        var quickFill = new QuickFillViewModel(
            root,
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
            new HealthViewModel(root, shell, services.HealthAuditService),
            new BackupCenterViewModel(root),
            new SettingsViewModel(root, shell, services.VaultRegistryService),
            new ActivityViewModel(root, services.ActivityRecorder.Store));
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
