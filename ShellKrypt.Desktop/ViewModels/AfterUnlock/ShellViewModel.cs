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

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    public ObservableCollection<NavItemVm> NavItems { get; } = new()
    {
        new NavItemVm("vault", "All Items"),
        new NavItemVm("web", "Web Logins"),
        new NavItemVm("cards", "Credit Cards"),
        new NavItemVm("api", "API Keys"),
        new NavItemVm("auth", "Authenticator"),
        new NavItemVm("notes", "Markdown Notes"),
        new NavItemVm("generator", "Generator"),
        new NavItemVm("audit", "Security Audit"),
        new NavItemVm("settings", "Settings"),
        new NavItemVm("activity", "Activity Logs"),
    };

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

        SelectNav("vault");
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
        "vault" => "All encrypted records in the active workspace.",
        "web" => "Credentials, account URLs, and saved login details.",
        "notes" => "Encrypted markdown notes and vault reference material.",
        "cards" => "Sensitive payment details protected in the vault.",
        "audit" => "Audit reuse, age, and password risk across the vault.",
        "generator" => "Generate and transform local secrets without leaving the vault.",
        "auth" => "Desktop authenticator codes from QR screenshots or pasted secret keys.",
        "api" => "API tokens, client secrets, project IDs, and provider metadata.",
        "settings" => "Manage vault security, import/export, and desktop behavior.",
        "activity" => "Review vault activity events and plaintext report exports.",
        _ => "Local encrypted vault workspace."
    };
    public bool IsSettingsSelected => SelectedNav?.Key == "settings";
    public bool ShowAddItemAction => !IsSettingsSelected;
    public string SearchPlaceholder => SelectedNav?.Key switch
    {
        "settings" => "Search settings...",
        "vault" => "Search all items...",
        "web" => "Search web logins...",
        "notes" => "Search markdown notes...",
        "cards" => "Search credit cards...",
        "audit" => "Search security audit...",
        "generator" => "Search generator tools...",
        "auth" => "Search authenticator codes...",
        "api" => "Search API keys...",
        "activity" => "Search activity...",
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
            "vault" => AllItems,
            "web" => WebLogins,
            "notes" => MarkdownNotes,
            "cards" => Cards,
            "generator" => Tools,
            "audit" => Health,
            "auth" => Authenticator,
            "api" => ApiKeys,
            "settings" => Settings,
            "activity" => Activity,
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
        CurrentPage = AllItems;
    }

    public void ShowWebLogins() => SelectNav("web");
    public async Task<bool> ShowWebLoginForRemediationAsync(string itemId, bool generateReplacementPassword = false)
    {
        SelectNav("web");
        return await WebLogins.OpenForRemediationAsync(itemId, generateReplacementPassword);
    }

    public void ShowCards() => SelectNav("cards");
    public void ShowMarkdownNotes() => SelectNav("notes");
    public void ShowSecurityAudit() => SelectNav("audit");
    public void ShowAuthenticator() => SelectNav("auth");
    public async Task<bool> ShowAuthenticatorByIdAsync(string itemId)
    {
        SelectNav("auth");
        return await Authenticator.OpenEntryByIdAsync(itemId);
    }
    public void ShowApiKeys() => SelectNav("api");
    public async Task<bool> ShowApiKeyByIdAsync(string itemId)
    {
        SelectNav("api");
        return await ApiKeys.OpenEntryByIdAsync(itemId);
    }
    public void ShowSettings() => SelectNav("settings");
    public void ShowActivity() => SelectNav("activity");

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
