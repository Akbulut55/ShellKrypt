using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Core.Vaulting;

public interface IVaultService
{
    Task CreateAsync(string vaultPath, string masterPassword, CancellationToken ct = default);
    Task<UnlockResult> UnlockAsync(string vaultPath, string masterPassword, CancellationToken ct = default);
}