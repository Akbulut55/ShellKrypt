using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Tools;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    public ObservableCollection<NavItemVm> NavItems { get; } = new()
    {
        new NavItemVm("vault", "Vault"),
        new NavItemVm("web", "Web Logins"),
        new NavItemVm("notes", "Secure Notes"),
        new NavItemVm("cards", "Credit Cards"),
        new NavItemVm("audit", "Security Audit"),
        new NavItemVm("generator", "Generator"),
        new NavItemVm("settings", "Settings"),
        new NavItemVm("activity", "Activity"),
    };

    [ObservableProperty] private NavItemVm? selectedNav;
    [ObservableProperty] private ViewModelBase currentPage = null!;

    public ShellViewModel(
        MainWindowViewModel root,
        IItemRepository repo,
        IWebLoginService webLoginService,
        ICardService cardService,
        ICryptoToolsService cryptoToolsService)
    {
        _root = root;

        AllItems = new AllItemsViewModel(_root, this, repo);
        WebLogins = new WebLoginsViewModel(_root, webLoginService);
        SecureNotes = new SecureNotesViewModel(_root, repo);
        Cards = new CardsViewModel(_root, cardService);
        Tools = new ToolsViewModel(_root, cryptoToolsService);
        Health = new HealthViewModel(_root, repo);
        Settings = new SettingsViewModel(_root);
        Vault = new PlaceholderPageViewModel(
            "Vault",
            "Vault overview placeholder. This top-level tab can hold the Stitch vault dashboard when that screen is ready.");
        Activity = new PlaceholderPageViewModel(
            "Activity",
            "Activity timeline placeholder. The Stitch activity log screen can land here when this section is implemented.");

        SelectNav("generator");
    }

    public WebLoginsViewModel WebLogins { get; }
    public SecureNotesViewModel SecureNotes { get; }
    public CardsViewModel Cards { get; }
    public ToolsViewModel Tools { get; }
    public HealthViewModel Health { get; }
    public AllItemsViewModel AllItems { get; }
    public SettingsViewModel Settings { get; }
    public PlaceholderPageViewModel Vault { get; }
    public PlaceholderPageViewModel Activity { get; }
    public string VaultName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? "Vault"
        : Path.GetFileNameWithoutExtension(_root.VaultPath);
    public string VaultSubtitle => "Local encrypted workspace";
    public string CurrentSectionTitle => SelectedNav?.Title ?? "ShellKrypt";
    public string CurrentSectionSubtitle => SelectedNav?.Key switch
    {
        "vault" => "Vault dashboard placeholder for the active encrypted workspace.",
        "web" => "Credentials, account URLs, and saved login details.",
        "notes" => "Encrypted private notes and vault reference material.",
        "cards" => "Sensitive payment details protected in the vault.",
        "audit" => "Audit reuse, age, and password risk across the repository.",
        "generator" => "Generate and transform local secrets without leaving the vault.",
        "settings" => "Manage security posture, import/export, and desktop behavior.",
        "activity" => "Activity log placeholder for future vault events.",
        _ => "Local encrypted vault workspace."
    };
    public bool IsSettingsSelected => SelectedNav?.Key == "settings";
    public bool ShowAddItemAction => !IsSettingsSelected;
    public string SearchPlaceholder => SelectedNav?.Key switch
    {
        "settings" => "Search settings...",
        "vault" => "Search vault...",
        "web" => "Search web logins...",
        "notes" => "Search secure notes...",
        "cards" => "Search credit cards...",
        "audit" => "Search security audit...",
        "generator" => "Search generator tools...",
        "activity" => "Search activity...",
        _ => "Search vault..."
    };

    partial void OnSelectedNavChanged(NavItemVm? value)
    {
        if (value is null)
            return;

        CurrentPage = value.Key switch
        {
            "vault" => Vault,
            "web" => WebLogins,
            "notes" => SecureNotes,
            "cards" => Cards,
            "generator" => Tools,
            "audit" => Health,
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

    [RelayCommand]
    private void Lock() => _root.Lock();

    public void ShowAllItems()
    {
        CurrentPage = AllItems;
    }

    public void ShowWebLogins() => SelectNav("web");
    public void ShowCards() => SelectNav("cards");
    public void ShowSecureNotes() => SelectNav("notes");

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
