using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.ViewModels;

public partial class WelcomeViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;

    [ObservableProperty]
    private string status = "Create a new vault or open the default vault.";

    public WelcomeViewModel(MainWindowViewModel root) => _root = root;

    [RelayCommand]
    private void CreateVault() => _root.GoCreateVault();

    [RelayCommand]
    private void OpenVault()
    {
        var path = DefaultPaths.DefaultVaultPath;

        if (!File.Exists(path))
        {
            Status = $"No vault found at:\n{path}\n\nCreate one first.";
            return;
        }

        _root.SetVaultPath(path);
        _root.GoUnlock();
    }
}