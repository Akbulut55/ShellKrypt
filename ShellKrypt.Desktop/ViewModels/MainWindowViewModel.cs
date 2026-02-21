using CommunityToolkit.Mvvm.ComponentModel;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Vaulting;

namespace ShellKrypt.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppState _state = new();
    private readonly IVaultService _vaultService = new SqliteVaultService();

    [ObservableProperty]
    private ViewModelBase current = null!;

    public MainWindowViewModel()
    {
        Current = new WelcomeViewModel(this);
    }

    public string? VaultPath => _state.VaultPath;

    public void SetVaultPath(string path) => _state.VaultPath = path;

    public void GoWelcome() => Current = new WelcomeViewModel(this);

    public void GoCreateVault() => Current = new CreateVaultViewModel(this, _vaultService);

    public void GoUnlock() => Current = new UnlockViewModel(this, _vaultService);

    public void OnUnlocked(byte[] vaultKey)
    {
        _state.VaultKey = vaultKey;
        Current = new ShellViewModel(this);
    }

    public void Lock()
    {
        _state.ClearSensitive();
        GoWelcome();
    }
}