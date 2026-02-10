using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ShellKrypt.Desktop.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private string error = "";

    public UnlockViewModel(MainWindowViewModel root) => _root = root;

    [RelayCommand]
    private void Unlock()
    {
        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            Error = "Enter a master password (real unlock in Step 2).";
            return;
        }

        Error = "";
        _root.NavigateTo(new ShellViewModel(_root));
    }

    [RelayCommand] private void Back() => _root.NavigateTo(new WelcomeViewModel(_root));
}