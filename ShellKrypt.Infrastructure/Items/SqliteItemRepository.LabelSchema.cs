using Microsoft.Data.Sqlite;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class SqliteItemRepository
{
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
            await EncryptLegacyLabelNameAsync(conn, vaultKey, row, ct);

        foreach (var row in rows.Where(row => row.EncryptedName is { Length: > 0 }))
            await ReconcileLabelLookupAsync(conn, vaultKey, row, ct);
    }

    private static async Task EncryptLegacyLabelNameAsync(SqliteConnection conn, byte[] vaultKey, StoredLabelRow row, CancellationToken ct)
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

    private static async Task ReconcileLabelLookupAsync(SqliteConnection conn, byte[] vaultKey, StoredLabelRow row, CancellationToken ct)
    {
        var decryptedName = VaultPayloadProtector.DecryptLabelName(vaultKey, row.Id, row.EncryptedName, row.LegacyName);
        var expectedLookup = ComputeLabelLookupKey(decryptedName);
        if (string.Equals(row.LegacyName, expectedLookup, StringComparison.Ordinal))
            return;

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
