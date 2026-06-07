using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class SqliteItemRepository
{
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

    private sealed record StoredLabelRow(
        string Id,
        byte[]? EncryptedName,
        string? LegacyName,
        string? Color);
}
