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
        new NavItemVm("settings", "Settings"),
    };

    [ObservableProperty] private NavItemVm? selectedNav;
    [ObservableProperty] private ViewModelBase currentPage = null!;

    public ShellViewModel(MainWindowViewModel root, IItemRepository repo)
    {
        _root = root;
        _repo = repo;

        // Pages
        WebLogins = new WebLoginsViewModel(_root, _repo);
        SecureNotes = new SecureNotesViewModel(_root, _repo);

        SelectedNav = NavItems[1]; // default: Web Logins
    }

    public WebLoginsViewModel WebLogins { get; }
    public SecureNotesViewModel SecureNotes { get; }

    partial void OnSelectedNavChanged(NavItemVm? value)
    {
        if (value is null) return;

        CurrentPage = value.Key switch
        {
            "web" => WebLogins,
            "notes" => SecureNotes,
            // placeholders for now:
            "cards" => WebLogins,
            "all" => WebLogins,
            _ => WebLogins
        };
    }

    [RelayCommand]
    private void Lock() => _root.Lock();
}