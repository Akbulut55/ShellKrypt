using ShellKrypt.Application.Vaulting;

namespace ShellKrypt.Application.Ports;

public interface IVaultRegistryStore
{
    VaultRegistry Load();
    void Save(VaultRegistry registry);
}
