using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Items;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Tools;
using ShellKrypt.Desktop.Services;
using ShellKrypt.UI.Shared.Navigation;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    public ObservableCollection<NavItemVm> NavItems { get; } = new();
    public ObservableCollection<NavItemVm> VisibleNavItems { get; } = new();
    public ObservableCollection<NavGroupVm> NavGroups { get; } = new();

    [ObservableProperty] private NavItemVm? selectedNav;
    [ObservableProperty] private ViewModelBase currentPage = null!;
    [ObservableProperty] private bool isSidebarCollapsed;

    public ShellViewModel(
        MainWindowViewModel root,
        IVaultItemSummaryService vaultItemSummaryService,
        IWebLoginService webLoginService,
        ICardService cardService,
        INoteService noteService,
        IAuthenticatorService authenticatorService,
        IApiKeyService apiKeyService,
        IQuickFillEntryService quickFillEntryService,
        AuthenticatorQrImportService authenticatorQrImportService,
        IHealthAuditService healthAuditService,
        ICryptoToolsService cryptoToolsService,
        ActivityLogService activityLogService,
        VaultRegistryService vaultRegistryService)
    {
        _root = root;
        var navItemsByKey = new Dictionary<string, NavItemVm>();
        foreach (var section in ShellKryptSectionCatalog.DesktopSections)
        {
            var item = new NavItemVm(section, _root.Localization);
            navItemsByKey[section.Key] = item;
            NavItems.Add(item);
            if (section.Key != ShellKryptSectionKeys.Settings)
                VisibleNavItems.Add(item);
        }

        SettingsNavItem = navItemsByKey[ShellKryptSectionKeys.Settings];
        foreach (var group in ShellKryptSectionCatalog.DesktopSections
                     .Where(section => section.Key != ShellKryptSectionKeys.Settings)
                     .GroupBy(section => section.Group))
        {
            NavGroups.Add(new NavGroupVm(group.Key, group.Select(section => navItemsByKey[section.Key]), _root.Localization));
        }

        AllItems = new AllItemsViewModel(_root, this, vaultItemSummaryService);
        WebLogins = new WebLoginsViewModel(_root, webLoginService, AllItems.RefreshAfterMutationAsync);
        MarkdownNotes = new MarkdownNotesViewModel(_root, noteService, AllItems.RefreshAfterMutationAsync);
        Cards = new CardsViewModel(_root, cardService, AllItems.RefreshAfterMutationAsync);
        Authenticator = new AuthenticatorViewModel(_root, authenticatorService, authenticatorQrImportService, AllItems.RefreshAfterMutationAsync);
        ApiKeys = new ApiKeysViewModel(_root, apiKeyService, AllItems.RefreshAfterMutationAsync);
        Tools = new ToolsViewModel(_root, cryptoToolsService);
        QuickFill = new QuickFillViewModel(_root, quickFillEntryService, webLoginService, apiKeyService, authenticatorService);
        Health = new HealthViewModel(_root, this, healthAuditService);
        EmergencyKit = new EmergencyKitViewModel(_root, this);
        BackupCenter = new BackupCenterViewModel(_root);
        Settings = new SettingsViewModel(_root, this, vaultRegistryService);
        Activity = new ActivityViewModel(_root, activityLogService);

        SelectNav(ShellKryptSectionKeys.Vault);
    }

    public WebLoginsViewModel WebLogins { get; }
    public MarkdownNotesViewModel MarkdownNotes { get; }
    public CardsViewModel Cards { get; }
    public AuthenticatorViewModel Authenticator { get; }
    public ApiKeysViewModel ApiKeys { get; }
    public ToolsViewModel Tools { get; }
    public QuickFillViewModel QuickFill { get; }
    public HealthViewModel Health { get; }
    public EmergencyKitViewModel EmergencyKit { get; }
    public BackupCenterViewModel BackupCenter { get; }
    public AllItemsViewModel AllItems { get; }
    public SettingsViewModel Settings { get; }
    public ActivityViewModel Activity { get; }
    public NavItemVm SettingsNavItem { get; }

    public override void RefreshLocalization()
    {
        foreach (var item in NavItems)
            item.RefreshLocalization();
        foreach (var group in NavGroups)
            group.RefreshLocalization();

        OnPropertyChanged(nameof(VaultSubtitle));
        OnPropertyChanged(nameof(VaultFooterLabel));
        OnPropertyChanged(nameof(SidebarToggleToolTip));
        OnPropertyChanged(nameof(CurrentSectionTitle));
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
        OnPropertyChanged(nameof(SearchPlaceholder));

        AllItems.RefreshLocalization();
        WebLogins.RefreshLocalization();
        MarkdownNotes.RefreshLocalization();
        Cards.RefreshLocalization();
        Authenticator.RefreshLocalization();
        ApiKeys.RefreshLocalization();
        Tools.RefreshLocalization();
        QuickFill.RefreshLocalization();
        Health.RefreshLocalization();
        EmergencyKit.RefreshLocalization();
        BackupCenter.RefreshLocalization();
        Settings.RefreshLocalization();
        Activity.RefreshLocalization();
    }
}
