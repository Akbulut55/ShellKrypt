using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IItemRepository _repo;

    public ObservableCollection<NavItemVm> NavItems { get; } = new()
    {
        new NavItemVm("all", "All Items"),
        new NavItemVm("web", "Web Logins"),
        new NavItemVm("cards", "Credit Cards"),
        new NavItemVm("notes", "Secure Notes"),
        new NavItemVm("tools", "Tools"),
        new NavItemVm("health", "Health"),
        new NavItemVm("settings", "Settings"),
    };

    [ObservableProperty] private NavItemVm? selectedNav;
    [ObservableProperty] private ViewModelBase currentPage = null!;

    public ShellViewModel(MainWindowViewModel root, IItemRepository repo)
    {
        _root = root;
        _repo = repo;

        AllItems = new AllItemsViewModel(_root, this, _repo);
        WebLogins = new WebLoginsViewModel(_root, _repo);
        SecureNotes = new SecureNotesViewModel(_root, _repo);
        Cards = new CardsViewModel(_root, _repo);
        Tools = new ToolsViewModel();
        Health = new HealthViewModel(_root, _repo);
        Settings = new SettingsViewModel(_root);

        SelectedNav = NavItems[1];
    }

    public WebLoginsViewModel WebLogins { get; }
    public SecureNotesViewModel SecureNotes { get; }
    public CardsViewModel Cards { get; }
    public ToolsViewModel Tools { get; }
    public HealthViewModel Health { get; }
    public AllItemsViewModel AllItems { get; }
    public SettingsViewModel Settings { get; }
    public string VaultName => string.IsNullOrWhiteSpace(_root.VaultPath)
        ? "Vault"
        : Path.GetFileNameWithoutExtension(_root.VaultPath);
    public string VaultSubtitle => "Local-first encrypted workspace";
    public string CurrentSectionTitle => SelectedNav?.Title ?? "ShellKrypt";
    public string CurrentSectionSubtitle => SelectedNav?.Key switch
    {
        "all" => "Unified inventory across the active vault.",
        "web" => "Credentials, authenticator secrets, and account details.",
        "cards" => "Sensitive payment details protected in the vault.",
        "notes" => "Secure notes in a calm master-detail workspace.",
        "tools" => "Local utilities for password, hash, and Base64 workflows.",
        "health" => "Credential hygiene, reuse, and age analysis.",
        "settings" => "Security, transfer, and vault preferences.",
        _ => "Protected desktop password manager."
    };

    partial void OnSelectedNavChanged(NavItemVm? value)
    {
        if (value is null)
            return;

        CurrentPage = value.Key switch
        {
            "all" => AllItems,
            "web" => WebLogins,
            "notes" => SecureNotes,
            "cards" => Cards,
            "tools" => Tools,
            "health" => Health,
            "settings" => Settings,
            _ => WebLogins
        };

        OnPropertyChanged(nameof(CurrentSectionTitle));
        OnPropertyChanged(nameof(CurrentSectionSubtitle));
    }

    [RelayCommand]
    private void Lock() => _root.Lock();

    public void ShowAllItems() => SelectedNav = NavItems[0];
    public void ShowWebLogins() => SelectedNav = NavItems[1];
    public void ShowCards() => SelectedNav = NavItems[2];
    public void ShowSecureNotes() => SelectedNav = NavItems[3];
}
