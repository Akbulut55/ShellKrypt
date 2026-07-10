using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    private static async Task ImportSnapshotTransactionalAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct)
    {
        vaultPath = VaultFileGuard.EnsureExistingVaultFile(vaultPath);
        await using var conn = await OpenVaultConnectionAsync(vaultPath, ct);
        await EnsureLabelSchemaAsync(conn, vaultKey, ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            var labelMap = await UpsertSnapshotLabelsAsync(conn, tx, vaultKey, snapshot.Labels, ct);
            var existingItemIds = await ReadItemIdsAsync(conn, tx, ct);

            foreach (var item in snapshot.Items)
            {
                if (existingItemIds.Contains(item.Id))
                    await DeleteItemAsync(conn, tx, item.Id, ct);

                var header = new VaultItemHeader(item.Id, item.Type, item.Favorite, item.CreatedAtUtc, item.UpdatedAtUtc);
                await InsertItemAsync(conn, tx, vaultKey, header, item.PayloadJson, ct);
                existingItemIds.Add(item.Id);
            }

            foreach (var item in snapshot.Items)
            {
                var labelIds = snapshot.ItemLabels
                    .Where(x => x.ItemId == item.Id)
                    .Select(x => labelMap.TryGetValue(x.LabelId, out var mappedId) ? mappedId : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                foreach (var labelId in labelIds)
                    await InsertItemLabelAsync(conn, tx, item.Id, labelId, ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task ImportCsvTransactionalAsync(string vaultPath, byte[] vaultKey, IReadOnlyList<CsvImportAction> actions, CancellationToken ct)
    {
        if (actions.Count == 0)
            return;

        vaultPath = VaultFileGuard.EnsureExistingVaultFile(vaultPath);
        await using var conn = await OpenVaultConnectionAsync(vaultPath, ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            foreach (var action in actions)
            {
                if (!string.IsNullOrWhiteSpace(action.DeleteItemId))
                    await DeleteItemAsync(conn, tx, action.DeleteItemId, ct);

                var candidate = action.Candidate;
                var header = new VaultItemHeader(candidate.Id, candidate.Type, false, candidate.CreatedAtUtc, candidate.UpdatedAtUtc);
                await InsertItemAsync(conn, tx, vaultKey, header, candidate.PayloadJson, ct);
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
