using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Backups.Internal;

namespace ShellKrypt.Infrastructure.DataTransfer.Internal;

internal sealed partial class VaultCsvImportProcessor
{
    private static async Task ImportCsvTransactionalAsync(
        string vaultPath,
        byte[] vaultKey,
        IReadOnlyList<CsvImportAction> actions,
        CancellationToken ct)
    {
        if (actions.Count == 0)
            return;

        vaultPath = VaultFileGuard.EnsureExistingVaultFile(vaultPath);
        await using var conn = await SqliteVaultSnapshotStore.OpenVaultConnectionAsync(vaultPath, ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(action.DeleteItemId))
                    await SqliteVaultSnapshotStore.DeleteItemAsync(conn, tx, action.DeleteItemId, ct);

                var candidate = action.Candidate;
                var header = new VaultItemHeader(candidate.Id, candidate.Type, false, candidate.CreatedAtUtc, candidate.UpdatedAtUtc);
                await SqliteVaultSnapshotStore.InsertItemAsync(conn, tx, vaultKey, header, candidate.PayloadJson, ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
