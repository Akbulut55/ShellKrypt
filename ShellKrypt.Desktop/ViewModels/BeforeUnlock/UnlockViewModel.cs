using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.Services;

namespace ShellKrypt.Desktop.ViewModels;

public partial class UnlockViewModel : ViewModelBase
{
    private const string LegacyDescription = "Legacy default vault";
    private const string DefaultUnlockDescription = "Unlock this local vault to continue securely.";
    private readonly MainWindowViewModel _root;
    private readonly IVaultService _vaultService;
    private readonly VaultRegistryStore _vaultRegistry;

    [ObservableProperty] private string masterPassword = "";
    [ObservableProperty] private string error = "";

    public string VaultTitle => _vaultRegistry.FindByPath(_root.VaultPath ?? "")?.DisplayName ?? "Unlock Vault";
    public string VaultPath => _root.VaultPath ?? "(no vault selected)";
    public string VaultDescription
    {
        get
        {
            var description = _vaultRegistry.FindByPath(_root.VaultPath ?? "")?.Description?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(description) ||
                string.Equals(description, LegacyDescription, System.StringComparison.OrdinalIgnoreCase))
            {
                return DefaultUnlockDescription;
            }

            return description;
        }
    }

    public UnlockViewModel(MainWindowViewModel root, IVaultService vaultService, VaultRegistryStore vaultRegistry)
    {
        _root = root;
        _vaultService = vaultService;
        _vaultRegistry = vaultRegistry;
    }

    [RelayCommand]
    private async Task UnlockAsync()
    {
        try
        {
            Error = "";

            if (_root.VaultPath is null)
            {
                Error = "No vault selected. Go back and choose a vault.";
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
