using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net.NetworkInformation;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    [ObservableProperty]
    private string status = "v1 shell: navigation only (no vault yet).";

    public WelcomeViewModel(MainWindowViewModel root) => _root = root;

    [RelayCommand] private void CreateVault() => Status = "Create Vault (Step 2).";
    [RelayCommand] private void OpenVault() => Status = "Open Vault (Step 2).";
    [RelayCommand] private void GoToUnlock() => _root.NavigateTo(new UnlockViewModel(_root));
}