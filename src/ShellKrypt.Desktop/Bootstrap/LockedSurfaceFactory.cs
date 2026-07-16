using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.ViewModels;

namespace ShellKrypt.Desktop.Bootstrap;

internal sealed class LockedSurfaceFactory(IVaultService vaultService, VaultRegistryService vaultRegistryService)
{
    public WelcomeViewModel CreateWelcome(MainWindowViewModel root) => new(root, vaultRegistryService);

    public CreateVaultViewModel CreateCreateVault(MainWindowViewModel root) => new(root, vaultService, vaultRegistryService);

    public UnlockViewModel CreateUnlock(MainWindowViewModel root) => new(root, vaultService, vaultRegistryService);
}
