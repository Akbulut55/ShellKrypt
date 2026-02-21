using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Core.Items;

public interface IItemRepository
{
    Task<IReadOnlyList<VaultItemRow>> ListAsync(string vaultPath, CancellationToken ct = default);
    Task InsertAsync(string vaultPath, VaultItemHeader header, byte[] encryptedPayload, CancellationToken ct = default);
    Task UpdateAsync(string vaultPath, VaultItemHeader header, byte[] encryptedPayload, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}