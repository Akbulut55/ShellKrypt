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
}
