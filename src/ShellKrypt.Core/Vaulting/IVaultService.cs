using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Core.Vaulting;

public interface IVaultService
{
    Task CreateAsync(string vaultPath, string masterPassword, VaultKdfParams? kdf = null, CancellationToken ct = default);
    Task<UnlockResult> UnlockAsync(string vaultPath, string masterPassword, CancellationToken ct = default);
    Task<ChangeMasterPasswordResult> ChangeMasterPasswordAsync(string vaultPath, string currentMasterPassword, string newMasterPassword, VaultKdfParams? newKdf = null, CancellationToken ct = default);
    Task<VaultKdfParams?> GetKdfParamsAsync(string vaultPath, CancellationToken ct = default);
}
