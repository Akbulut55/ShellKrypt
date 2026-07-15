using Microsoft.Data.Sqlite;

namespace ShellKrypt.Infrastructure.Backups.Internal;

internal sealed partial class SqliteVaultSnapshotStore
{
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
