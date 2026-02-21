using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShellKrypt.Desktop.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    public ObservableCollection<string> Sidebar { get; } = new()
    {
        "All Items","Web","Cards","Notes","Labels","Tools","Health","Settings"
    };

    public ObservableCollection<string> DemoList { get; } = new()
    {
        "Demo item 1","Demo item 2","Demo item 3"
    };

    [ObservableProperty] private string selectedSection = "All Items";
    [ObservableProperty] private string searchText = "";

    public ShellViewModel(MainWindowViewModel root) => _root = root;

    [RelayCommand]
    private void Lock() => _root.Lock();
}