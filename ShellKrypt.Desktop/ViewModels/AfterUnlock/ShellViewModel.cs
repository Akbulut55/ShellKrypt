using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public ObservableCollection<NavItemVm> NavItems { get; } = new(
        ShellKryptSectionCatalog.DesktopSections.Select(section => new NavItemVm(section)));

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
        AuthenticatorQrImportService authenticatorQrImportService,
        IHealthAuditService healthAuditService,
        ICryptoToolsService cryptoToolsService,
        ActivityLogService activityLogService,
        AuditDismissalService auditDismissalService,
        VaultRegistryService vaultRegistryService)
    {
        _root = root;

        AllItems = new AllItemsViewModel(_root, this, vaultItemSummaryService);
        WebLogins = new WebLoginsViewModel(_root, webLoginService, AllItems.RefreshAfterMutationAsync);
        MarkdownNotes = new MarkdownNotesViewModel(_root, noteService, AllItems.RefreshAfterMutationAsync);
        Cards = new CardsViewModel(_root, cardService, AllItems.RefreshAfterMutationAsync);
        Authenticator = new AuthenticatorViewModel(_root, authenticatorService, authenticatorQrImportService, AllItems.RefreshAfterMutationAsync);
        ApiKeys = new ApiKeysViewModel(_root, apiKeyService, AllItems.RefreshAfterMutationAsync);
        Tools = new ToolsViewModel(_root, cryptoToolsService);
        Health = new HealthViewModel(_root, this, healthAuditService, auditDismissalService);
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
    public HealthViewModel Health { get; }
    public AllItemsViewModel AllItems { get; }
    public SettingsViewModel Settings { get; }
    public ActivityViewModel Activity { get; }
    public string VaultName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? "Vault"
        : Path.GetFileNameWithoutExtension(_root.VaultPath);
    public string VaultSubtitle => "Current encrypted workspace";
    public string VaultMonogram
    {
        get
        {
            var letters = VaultName
                .Where(char.IsLetterOrDigit)
                .Take(2)
                .ToArray();

            return letters.Length == 0
                ? "VA"
                : new string(letters).ToUpperInvariant();
        }
    }
    public string VaultFooterLabel => "ACTIVE VAULT";
    public bool IsSidebarExpanded => !IsSidebarCollapsed;
    public double SidebarWidth => IsSidebarCollapsed ? 96 : 236;
    public string SidebarToggleToolTip => IsSidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";
    public string CurrentSectionTitle => SelectedNav?.Title ?? "ShellKrypt";
    public string CurrentSectionSubtitle => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Vault => "All encrypted records in the active workspace.",
        ShellKryptSectionKeys.WebLogins => "Credentials, account URLs, and saved login details.",
        ShellKryptSectionKeys.Notes => "Encrypted markdown notes and vault reference material.",
        ShellKryptSectionKeys.Cards => "Sensitive payment details protected in the vault.",
        ShellKryptSectionKeys.Audit => "Audit reuse, age, and password risk across the vault.",
        ShellKryptSectionKeys.Generator => "Generate and transform local secrets without leaving the vault.",
        ShellKryptSectionKeys.Authenticator => "Desktop authenticator codes from QR screenshots or pasted secret keys.",
        ShellKryptSectionKeys.ApiKeys => "API tokens, client secrets, project IDs, and provider metadata.",
        ShellKryptSectionKeys.Settings => "Manage vault security, import/export, and desktop behavior.",
        ShellKryptSectionKeys.Activity => "Review vault activity events and plaintext report exports.",
        _ => "Local encrypted vault workspace."
    };
    public bool IsSettingsSelected => SelectedNav?.Key == ShellKryptSectionKeys.Settings;
    public bool ShowAddItemAction => !IsSettingsSelected;
    public string SearchPlaceholder => SelectedNav?.Key switch
    {
        ShellKryptSectionKeys.Settings => "Search settings...",
        ShellKryptSectionKeys.Vault => "Search all items...",
        ShellKryptSectionKeys.WebLogins => "Search web logins...",
        ShellKryptSectionKeys.Notes => "Search markdown notes...",
        ShellKryptSectionKeys.Cards => "Search credit cards...",
        ShellKryptSectionKeys.Audit => "Search security audit...",
        ShellKryptSectionKeys.Generator => "Search generator tools...",
        ShellKryptSectionKeys.Authenticator => "Search authenticator codes...",
        ShellKryptSectionKeys.ApiKeys => "Search API keys...",
        ShellKryptSectionKeys.Activity => "Search activity...",
        _ => "Search all items..."
    };

    partial void OnSelectedNavChanged(NavItemVm? value)
    {
        if (value is null)
            return;

        foreach (var item in NavItems)
            item.IsSelected = ReferenceEquals(item, value);

        CurrentPage = value.Key switch
        {
            ShellKryptSectionKeys.Vault => AllItems,
            ShellKryptSectionKeys.WebLogins => WebLogins,
            ShellKryptSectionKeys.Notes => MarkdownNotes,
            ShellKryptSectionKeys.Cards => Cards,
            ShellKryptSectionKeys.Generator => Tools,
            ShellKryptSectionKeys.Audit => Health,
            ShellKryptSectionKeys.Authenticator => Authenticator,
            ShellKryptSectionKeys.ApiKeys => ApiKeys,
            ShellKryptSectionKeys.Settings => Settings,
            ShellKryptSectionKeys.Activity => Activity,
            _ => AllItems
        };

        OnPropertyChanged(nameof(CurrentSectionTitle));
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(ShowAddItemAction));
        OnPropertyChanged(nameof(SearchPlaceholder));
    }

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSidebarExpanded));
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarToggleToolTip));
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void Lock() => _root.Lock();

    [RelayCommand]
    private void SelectSection(NavItemVm? item)
    {
        if (item is not null)
            SelectedNav = item;
    }

    public void ShowAllItems()
    {
        SelectNav(ShellKryptSectionKeys.Vault);
    }

    public void ShowWebLogins() => SelectNav(ShellKryptSectionKeys.WebLogins);
    public async Task<bool> ShowWebLoginForRemediationAsync(string itemId, bool generateReplacementPassword = false)
    {
        SelectNav(ShellKryptSectionKeys.WebLogins);
        return await WebLogins.OpenForRemediationAsync(itemId, generateReplacementPassword);
    }

    public void ShowCards() => SelectNav(ShellKryptSectionKeys.Cards);
    public void ShowMarkdownNotes() => SelectNav(ShellKryptSectionKeys.Notes);
    public void ShowSecurityAudit() => SelectNav(ShellKryptSectionKeys.Audit);
    public void ShowAuthenticator() => SelectNav(ShellKryptSectionKeys.Authenticator);
    public async Task<bool> ShowAuthenticatorByIdAsync(string itemId)
    {
        SelectNav(ShellKryptSectionKeys.Authenticator);
        return await Authenticator.OpenEntryByIdAsync(itemId);
    }
    public void ShowApiKeys() => SelectNav(ShellKryptSectionKeys.ApiKeys);
    public async Task<bool> ShowApiKeyByIdAsync(string itemId)
    {
        SelectNav(ShellKryptSectionKeys.ApiKeys);
        return await ApiKeys.OpenEntryByIdAsync(itemId);
    }
    public void ShowSettings() => SelectNav(ShellKryptSectionKeys.Settings);
    public void ShowActivity() => SelectNav(ShellKryptSectionKeys.Activity);

    private void SelectNav(string key)
    {
        foreach (var item in NavItems)
        {
            if (item.Key == key)
            {
                SelectedNav = item;
                return;
            }
        }
    }
}
