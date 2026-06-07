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
}
