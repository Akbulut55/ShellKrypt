using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Items;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Authenticator;
using ShellKrypt.Core.CryptoTools;
using ShellKrypt.Core.ProjectSecrets;
using ShellKrypt.Desktop.Bootstrap;
using ShellKrypt.Desktop.Shell.Runtime;
using ShellKrypt.Application.Localization;
using ShellKrypt.Desktop.Features.Authenticator;
using ShellKrypt.Desktop.Features.BackupCenter;
using ShellKrypt.Desktop.Features.ItemWorkspaces.ApiKeys;
using ShellKrypt.Desktop.Features.ItemWorkspaces.CreditCards;
using ShellKrypt.Desktop.Features.ItemWorkspaces.WebLogins;
using ShellKrypt.Desktop.Features.ProjectSecrets;
using ShellKrypt.Desktop.Shell;
using ShellKrypt.Desktop.Shell;

namespace ShellKrypt.Desktop.Shell;

public partial class ShellViewModel : ViewModelBase
{
    private readonly LocalizationService _localization;
    private readonly IVaultSessionController _session;
    private readonly IDesktopNavigation _navigation;

    public ObservableCollection<NavItemVm> NavItems { get; } = new();
    public ObservableCollection<NavItemVm> VisibleNavItems { get; } = new();
    public ObservableCollection<NavGroupVm> NavGroups { get; } = new();

    [ObservableProperty] private NavItemVm? selectedNav;
    [ObservableProperty] private ViewModelBase currentPage = null!;
    [ObservableProperty] private bool isSidebarCollapsed;

    internal ShellViewModel(
        LocalizationService localization,
        IVaultSessionController session,
        IDesktopNavigation navigation,
        Func<ShellViewModel, ShellWorkspaces> createWorkspaces)
    {
        _localization = localization;
        _session = session;
        _navigation = navigation;
        var navItemsByKey = new Dictionary<string, NavItemVm>();
        foreach (var section in ShellKryptSectionCatalog.DesktopSections)
        {
            var item = new NavItemVm(section, _localization);
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
            NavGroups.Add(new NavGroupVm(group.Key, group.Select(section => navItemsByKey[section.Key]), _localization));
        }

        var workspaces = createWorkspaces(this);
        AllItems = workspaces.AllItems;
        WebLogins = workspaces.WebLogins;
        MarkdownNotes = workspaces.MarkdownNotes;
        Cards = workspaces.Cards;
        Authenticator = workspaces.Authenticator;
        ApiKeys = workspaces.ApiKeys;
        ProjectSecrets = workspaces.ProjectSecrets;
        CryptoTools = workspaces.CryptoTools;
        Health = workspaces.Health;
        BackupCenter = workspaces.BackupCenter;
        Settings = workspaces.Settings;
        Activity = workspaces.Activity;

        SelectNav(ShellKryptSectionKeys.Vault);
    }

    public WebLoginsViewModel WebLogins { get; }
    public MarkdownNotesViewModel MarkdownNotes { get; }
    public CardsViewModel Cards { get; }
    public AuthenticatorViewModel Authenticator { get; }
    public ApiKeysViewModel ApiKeys { get; }
    public ProjectSecretsViewModel ProjectSecrets { get; }
    public CryptoToolsViewModel CryptoTools { get; }
    public HealthViewModel Health { get; }
    public BackupCenterViewModel BackupCenter { get; }
    public AllItemsViewModel AllItems { get; }
    public SettingsViewModel Settings { get; }
    public ActivityViewModel Activity { get; }
    public NavItemVm SettingsNavItem { get; }

    public void Deactivate()
    {
        Authenticator.Deactivate();
        Activity.Deactivate();
    }

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
        ProjectSecrets.RefreshLocalization();
        CryptoTools.RefreshLocalization();
        Health.RefreshLocalization();
        BackupCenter.RefreshLocalization();
        Settings.RefreshLocalization();
        Activity.RefreshLocalization();
    }
}
