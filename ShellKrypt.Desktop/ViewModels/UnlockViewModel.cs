using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;

    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private string error = "";

    public string VaultPath => _root.VaultPath ?? "(no vault selected)";

    public UnlockViewModel(MainWindowViewModel root, IVaultService vaultService)
    {
        _root = root;
        _vaultService = vaultService;
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        try
        {
            Error = "";

            if (_root.VaultPath is null)
            {
                Error = "No vault selected. Go back and Create/Open a vault.";
                return;
            }

            var result = await _vaultService.UnlockAsync(_root.VaultPath, MasterPassword);
            if (!result.Success)
            {
                Error = result.Error ?? "Unlock failed.";
                return;
            }

            _root.OnUnlocked(result.VaultKey!);
        }
        finally
        {
            MasterPassword = "";
        }
    }

    [RelayCommand]
    private void Back() => _root.GoWelcome();
}
