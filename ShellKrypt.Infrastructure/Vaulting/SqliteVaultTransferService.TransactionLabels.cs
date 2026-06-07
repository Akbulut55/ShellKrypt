using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
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
