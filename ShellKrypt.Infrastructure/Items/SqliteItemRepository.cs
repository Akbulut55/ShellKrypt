using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed class SqliteItemRepository : IItemRepository
{
    public async Task<IReadOnlyList<VaultItemRow>> ListAsync(string vaultPath, CancellationToken ct = default)
    {
        var items = new Dictionary<string, ItemRowBuilder>(StringComparer.Ordinal);
        var order = new List<string>();

        await using var conn = await OpenConnectionAsync(vaultPath, ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT i.id, i.type, i.favorite, i.createdAtUtc, i.updatedAtUtc, i.encryptedPayload,
               l.id, l.name, l.color
        FROM items i
        LEFT JOIN item_labels il ON il.itemId = i.id
        LEFT JOIN labels l ON l.id = il.labelId
        ORDER BY i.updatedAtUtc DESC, i.id ASC, l.name COLLATE NOCASE ASC;
        """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);

            if (!items.TryGetValue(id, out var builder))
            {
                builder = new ItemRowBuilder(
                    new VaultItemHeader(
                        id,
                        (ItemType)reader.GetInt32(1),
                        reader.GetInt32(2) != 0,
                        reader.GetString(3),
                        reader.GetString(4)),
                    reader.GetFieldValue<byte[]>(5));

                items[id] = builder;
                order.Add(id);
            }

            if (!reader.IsDBNull(6))
            {
                builder.Labels.Add(new VaultLabelRow(
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }
        }

        return order.Select(id => items[id].Build()).ToArray();
    }

    public async Task<IReadOnlyList<VaultLabelRow>> ListLabelsAsync(string vaultPath, CancellationToken ct = default)
    {
        var labels = new List<VaultLabelRow>();

        await using var conn = await OpenConnectionAsync(vaultPath, ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT id, name, color
        FROM labels
        ORDER BY name COLLATE NOCASE ASC;
        """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            labels.Add(new VaultLabelRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return labels;
    }

    public async Task<VaultLabelRow> UpsertLabelAsync(string vaultPath, string name, string? color = null, CancellationToken ct = default)
    {
        var normalized = NormalizeLabelName(name);
        if (normalized is null)
            throw new ArgumentException("Label name cannot be empty.", nameof(name));

        await using var conn = await OpenConnectionAsync(vaultPath, ct);

        var select = conn.CreateCommand();
        select.CommandText = """
        SELECT id, name, color
        FROM labels
        WHERE name COLLATE NOCASE = $name
        LIMIT 1;
        """;
        select.Parameters.AddWithValue("$name", normalized);

        await using (var reader = await select.ExecuteReaderAsync(ct))
        {
            if (await reader.ReadAsync(ct))
            {
                return new VaultLabelRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2));
            }
        }

        var id = Guid.NewGuid().ToString("N");
        var insert = conn.CreateCommand();
        insert.CommandText = """
        INSERT INTO labels (id, name, color)
        VALUES ($id, $name, $color);
        """;
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.AddWithValue("$name", normalized);
        insert.Parameters.AddWithValue("$color", string.IsNullOrWhiteSpace(color) ? DBNull.Value : color);
        await insert.ExecuteNonQueryAsync(ct);

        return new VaultLabelRow(id, normalized, string.IsNullOrWhiteSpace(color) ? null : color);
    }

    public async Task SetItemLabelsAsync(string vaultPath, string itemId, IReadOnlyCollection<string> labelIds, CancellationToken ct = default)
    {
        var ids = labelIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await using var conn = await OpenConnectionAsync(vaultPath, ct);
        await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

        var delete = conn.CreateCommand();
        delete.Transaction = tx;
        delete.CommandText = "DELETE FROM item_labels WHERE itemId = $itemId;";
        delete.Parameters.AddWithValue("$itemId", itemId);
        await delete.ExecuteNonQueryAsync(ct);

        foreach (var labelId in ids)
        {
            var insert = conn.CreateCommand();
            insert.Transaction = tx;
            insert.CommandText = """
            INSERT INTO item_labels (itemId, labelId)
            VALUES ($itemId, $labelId);
            """;
            insert.Parameters.AddWithValue("$itemId", itemId);
            insert.Parameters.AddWithValue("$labelId", labelId);
            await insert.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task InsertAsync(string vaultPath, VaultItemHeader header, byte[] encryptedPayload, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(vaultPath, ct);

        var cmd = conn.CreateCommand();
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

    public async Task UpdateAsync(string vaultPath, VaultItemHeader header, byte[] encryptedPayload, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(vaultPath, ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        UPDATE items
        SET favorite = $fav,
            updatedAtUtc = $updated,
            encryptedPayload = $payload
        WHERE id = $id;
        """;

        cmd.Parameters.AddWithValue("$id", header.Id);
        cmd.Parameters.AddWithValue("$fav", header.Favorite ? 1 : 0);
        cmd.Parameters.AddWithValue("$updated", header.UpdatedAtUtc);
        cmd.Parameters.Add("$payload", SqliteType.Blob).Value = encryptedPayload;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(vaultPath, ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(string vaultPath, CancellationToken ct)
    {
        var conn = new SqliteConnection($"Data Source={vaultPath};Mode=ReadWrite;");
        await conn.OpenAsync(ct);
        await EnableForeignKeysAsync(conn, ct);
        return conn;
    }

    private static async Task EnableForeignKeysAsync(SqliteConnection conn, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string? NormalizeLabelName(string name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed class ItemRowBuilder
    {
        public ItemRowBuilder(VaultItemHeader header, byte[] payload)
        {
            Header = header;
            Payload = payload;
        }

        public VaultItemHeader Header { get; }
        public byte[] Payload { get; }
        public List<VaultLabelRow> Labels { get; } = new();

        public VaultItemRow Build() => new(Header, Payload, Labels.ToArray());
    }
}
