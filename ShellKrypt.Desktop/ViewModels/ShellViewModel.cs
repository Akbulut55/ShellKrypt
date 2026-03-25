using System.Collections.ObjectModel;
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

        // Pages
        AllItems = new AllItemsViewModel(_root, this, _repo);
        WebLogins = new WebLoginsViewModel(_root, _repo);
        SecureNotes = new SecureNotesViewModel(_root, _repo);
        Cards = new CardsViewModel(_root, _repo);
        Tools = new ToolsViewModel();
        Health = new HealthViewModel(_root, _repo);
        Settings = new SettingsViewModel(_root);

        SelectedNav = NavItems[1]; // default: Web Logins
    }

    public WebLoginsViewModel WebLogins { get; }
    public SecureNotesViewModel SecureNotes { get; }
    public CardsViewModel Cards { get; }
    public ToolsViewModel Tools { get; }
    public HealthViewModel Health { get; }
    public AllItemsViewModel AllItems { get; }
    public SettingsViewModel Settings { get; }

    partial void OnSelectedNavChanged(NavItemVm? value)
    {
        if (value is null) return;

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
    }

    [RelayCommand]
    private void Lock() => _root.Lock();

    public void ShowAllItems() => SelectedNav = NavItems[0];
    public void ShowWebLogins() => SelectedNav = NavItems[1];
    public void ShowCards() => SelectedNav = NavItems[2];
    public void ShowSecureNotes() => SelectedNav = NavItems[3];
}
