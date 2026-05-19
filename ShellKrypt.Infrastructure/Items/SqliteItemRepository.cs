using System.Text;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class SqliteItemRepository : IItemRepository
{
    public async Task<IReadOnlyList<VaultItemRow>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var items = new Dictionary<string, ItemRowBuilder>(StringComparer.Ordinal);
        var order = new List<string>();

        await using var conn = await OpenConnectionAsync(vaultPath, ct);
        await EnsureLabelSchemaAsync(conn, vaultKey, ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT i.id, i.type, i.favorite, i.createdAtUtc, i.updatedAtUtc, i.encryptedPayload,
               l.id, l.encryptedName, l.name, l.color
        FROM items i
        LEFT JOIN item_labels il ON il.itemId = i.id
        LEFT JOIN labels l ON l.id = il.labelId
        ORDER BY i.updatedAtUtc DESC, i.id ASC;
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
                    VaultPayloadProtector.DecryptLabelName(vaultKey, reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<byte[]>(7), reader.IsDBNull(8) ? null : reader.GetString(8)),
                    reader.IsDBNull(9) ? null : reader.GetString(9)));
            }
        }

        return order
            .Select(id => items[id].Build())
            .Select(SortLabels)
            .ToArray();
    }

    public async Task<IReadOnlyList<VaultLabelRow>> ListLabelsAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var labels = new List<VaultLabelRow>();

        await using var conn = await OpenConnectionAsync(vaultPath, ct);
        await EnsureLabelSchemaAsync(conn, vaultKey, ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT id, encryptedName, name, color
        FROM labels
        ORDER BY id ASC;
        """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            labels.Add(new VaultLabelRow(
                reader.GetString(0),
                VaultPayloadProtector.DecryptLabelName(vaultKey, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetFieldValue<byte[]>(1), reader.IsDBNull(2) ? null : reader.GetString(2)),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return labels
            .OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<VaultLabelRow> UpsertLabelAsync(string vaultPath, byte[] vaultKey, string name, string? color = null, CancellationToken ct = default)
    {
        var normalized = NormalizeLabelName(name);
        if (normalized is null)
            throw new ArgumentException("Label name cannot be empty.", nameof(name));

        await using var conn = await OpenConnectionAsync(vaultPath, ct);
        await EnsureLabelSchemaAsync(conn, vaultKey, ct);

        var existing = await ReadStoredLabelsAsync(conn, ct);
        var match = existing.FirstOrDefault(label =>
            string.Equals(
                NormalizeLabelName(VaultPayloadProtector.DecryptLabelName(vaultKey, label.Id, label.EncryptedName, label.LegacyName)),
                normalized,
                StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            return new VaultLabelRow(
                match.Id,
                normalized,
                string.IsNullOrWhiteSpace(match.Color) ? null : match.Color);
        }

        var id = Guid.NewGuid().ToString("N");
        var insert = conn.CreateCommand();
        insert.CommandText = """
        INSERT INTO labels (id, encryptedName, name, color)
        VALUES ($id, $encryptedName, $lookup, $color);
        """;
        insert.Parameters.AddWithValue("$id", id);
        insert.Parameters.Add("$encryptedName", SqliteType.Blob).Value = VaultPayloadProtector.EncryptLabelName(vaultKey, id, normalized);
        insert.Parameters.AddWithValue("$lookup", ComputeLabelLookupKey(normalized));
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
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        var conn = new SqliteConnection(builder.ToString());
        await conn.OpenAsync(ct);
        await ConfigureConnectionAsync(conn, ct);
        return conn;
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection conn, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode=DELETE;
        """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string? NormalizeLabelName(string name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

        var rows = await ReadStoredLabelsAsync(conn, ct);
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

        foreach (var row in rows.Where(row => row.EncryptedName is { Length: > 0 }))
        {
            var decryptedName = VaultPayloadProtector.DecryptLabelName(vaultKey, row.Id, row.EncryptedName, row.LegacyName);
            var expectedLookup = ComputeLabelLookupKey(decryptedName);
            if (string.Equals(row.LegacyName, expectedLookup, StringComparison.Ordinal))
                continue;

            var update = conn.CreateCommand();
            update.CommandText = """
            UPDATE labels
            SET name = $lookup
            WHERE id = $id;
            """;
            update.Parameters.AddWithValue("$id", row.Id);
            update.Parameters.AddWithValue("$lookup", expectedLookup);
            await update.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<IReadOnlyList<StoredLabelRow>> ReadStoredLabelsAsync(SqliteConnection conn, CancellationToken ct)
    {
        var labels = new List<StoredLabelRow>();
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT id, encryptedName, name, color
        FROM labels
        ORDER BY id ASC;
        """;

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

    private static string ComputeLabelLookupKey(string name)
    {
        var normalized = NormalizeLabelName(name) ?? string.Empty;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant()));
        return Convert.ToHexString(hash);
    }

    private static VaultItemRow SortLabels(VaultItemRow row)
        => new(
            row.Header,
            row.EncryptedPayload,
            row.Labels.OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase).ToArray());

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

    private sealed record StoredLabelRow(
        string Id,
        byte[]? EncryptedName,
        string? LegacyName,
        string? Color);
}
