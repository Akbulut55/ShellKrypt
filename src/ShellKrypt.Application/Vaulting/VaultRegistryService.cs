using ShellKrypt.Application.Ports;

namespace ShellKrypt.Application.Vaulting;

public sealed partial class VaultRegistryService
{
    private readonly IVaultRegistryStore _store;

    public VaultRegistryService(IVaultRegistryStore store)
    {
        _store = store;
    }

    public VaultRegistry Load() => NormalizeRegistry(_store.Load());

    public void Save(VaultRegistry registry) => _store.Save(NormalizeRegistry(registry));
}
