using System.Text;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

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

    private static async Task<Dictionary<string, string>> UpsertSnapshotLabelsAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        byte[] vaultKey,
        IReadOnlyList<VaultSnapshotLabel> labels,
        CancellationToken ct)
    {
        var labelMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var existing = await ReadStoredLabelsAsync(conn, tx, ct);

        foreach (var label in labels)
        {
            var normalized = NormalizeLabelName(label.Name);
            if (normalized is null)
                continue;

            var match = existing.FirstOrDefault(row =>
                string.Equals(
                    NormalizeLabelName(VaultPayloadProtector.DecryptLabelName(vaultKey, row.Id, row.EncryptedName, row.LegacyName)),
                    normalized,
                    StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                labelMap[label.Id] = match.Id;
                continue;
            }

            var id = Guid.NewGuid().ToString("N");
            var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
            INSERT INTO labels (id, encryptedName, name, color)
            VALUES ($id, $encryptedName, $lookup, $color);
            """;
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.Add("$encryptedName", SqliteType.Blob).Value = VaultPayloadProtector.EncryptLabelName(vaultKey, id, normalized);
            insert.Parameters.AddWithValue("$lookup", ComputeLabelLookupKey(normalized));
            insert.Parameters.AddWithValue("$color", string.IsNullOrWhiteSpace(label.Color) ? DBNull.Value : label.Color);
            await insert.ExecuteNonQueryAsync(ct);

            existing.Add(new StoredLabelRow(id, VaultPayloadProtector.EncryptLabelName(vaultKey, id, normalized), ComputeLabelLookupKey(normalized), label.Color));
            labelMap[label.Id] = id;
        }

        return labelMap;
    }

    private static async Task<HashSet<string>> ReadItemIdsAsync(SqliteConnection conn, SqliteTransaction tx, CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id FROM items;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetString(0));

        return ids;
    }

    private static async Task DeleteItemAsync(SqliteConnection conn, SqliteTransaction tx, string itemId, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM items WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", itemId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertItemAsync(
        SqliteConnection conn,
        SqliteTransaction tx,
        byte[] vaultKey,
        VaultItemHeader header,
        string payloadJson,
        CancellationToken ct)
    {
        var encryptedPayload = VaultPayloadProtector.EncryptItemPayload(vaultKey, header, Encoding.UTF8.GetBytes(payloadJson));
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
        INSERT INTO items (id, type, favorite, createdAtUtc, updatedAtUtc, encryptedPayload)
        VALUES ($id, $type, $fav, $created, $updated, $payload);
        """;
        cmd.Parameters.AddWithValue("$id", header.Id);
        cmd.Parameters.AddWithValue("$type", (int)header.Type);
        cmd.Parameters.AddWithValue("$fav", header.Favorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$created", header.CreatedAtUtc);
        cmd.Parameters.AddWithValue("$updated", header.UpdatedAtUtc);
        cmd.Parameters.Add("$payload", SqliteType.Blob).Value = encryptedPayload;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertItemLabelAsync(SqliteConnection conn, SqliteTransaction tx, string itemId, string labelId, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
        INSERT OR IGNORE INTO item_labels (itemId, labelId)
        VALUES ($itemId, $labelId);
        """;
        cmd.Parameters.AddWithValue("$itemId", itemId);
        cmd.Parameters.AddWithValue("$labelId", labelId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<SqliteConnection> OpenVaultConnectionAsync(string vaultPath, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        var conn = new SqliteConnection(builder.ToString());
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode=DELETE;
        """;
        await cmd.ExecuteNonQueryAsync(ct);
        return conn;
    }

    private static async Task EnsureLabelSchemaAsync(SqliteConnection conn, byte[] vaultKey, CancellationToken ct)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(labels);";
        await using (var reader = await pragma.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("encryptedName"))
        {
            var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE labels ADD COLUMN encryptedName BLOB;";
            await alter.ExecuteNonQueryAsync(ct);
        }

        var rows = await ReadStoredLabelsAsync(conn, null, ct);
        foreach (var row in rows.Where(row => row.EncryptedName is null && !string.IsNullOrWhiteSpace(row.LegacyName)))
        {
            var update = conn.CreateCommand();
            update.CommandText = """
            UPDATE labels
            SET encryptedName = $encryptedName,
                name = $lookup
            WHERE id = $id;
            """;
            update.Parameters.AddWithValue("$id", row.Id);
            update.Parameters.Add("$encryptedName", SqliteType.Blob).Value = VaultPayloadProtector.EncryptLabelName(vaultKey, row.Id, row.LegacyName!);
            update.Parameters.AddWithValue("$lookup", ComputeLabelLookupKey(row.LegacyName!));
            await update.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<List<StoredLabelRow>> ReadStoredLabelsAsync(SqliteConnection conn, SqliteTransaction? tx, CancellationToken ct)
    {
        var labels = new List<StoredLabelRow>();
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT id, encryptedName, name, color FROM labels ORDER BY id ASC;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            labels.Add(new StoredLabelRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetFieldValue<byte[]>(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return labels;
    }
}
