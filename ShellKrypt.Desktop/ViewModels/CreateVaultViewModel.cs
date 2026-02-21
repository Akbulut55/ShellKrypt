using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using System;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ShellKrypt.Desktop.ViewModels;

public partial class CreateVaultViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;

    [ObservableProperty] private string vaultPath = DefaultPaths.DefaultVaultPath;
    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private string confirmPassword = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;

    public CreateVaultViewModel(MainWindowViewModel root, IVaultService vaultService)
    {
        _root = root;
        _vaultService = vaultService;
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = "";

        if (string.IsNullOrWhiteSpace(MasterPassword))
        {
            Error = "Master password is required.";
            return;
        }

        if (MasterPassword != ConfirmPassword)
        {
            Error = "Passwords do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            await _vaultService.CreateAsync(VaultPath, MasterPassword);
            _root.SetVaultPath(VaultPath);
            _root.GoUnlock();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
            MasterPassword = "";
            ConfirmPassword = "";
        }
    }

    [RelayCommand]
    private void Back() => _root.GoWelcome();
}