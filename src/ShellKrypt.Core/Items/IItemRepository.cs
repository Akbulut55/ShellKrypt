using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ShellKrypt.Core.Items;

public interface IItemRepository
{
    Task<IReadOnlyList<VaultItemRow>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<IReadOnlyList<VaultLabelRow>> ListLabelsAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<VaultLabelRow> UpsertLabelAsync(string vaultPath, byte[] vaultKey, string name, string? color = null, CancellationToken ct = default);
    Task SetItemLabelsAsync(string vaultPath, string itemId, IReadOnlyCollection<string> labelIds, CancellationToken ct = default);
    Task InsertAsync(string vaultPath, VaultItemHeader header, byte[] encryptedPayload, CancellationToken ct = default);
    Task UpdateAsync(string vaultPath, VaultItemHeader header, byte[] encryptedPayload, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}
