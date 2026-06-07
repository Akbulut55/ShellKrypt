using Microsoft.Data.Sqlite;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class SqliteItemRepository
{
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

    private sealed record StoredLabelRow(
        string Id,
        byte[]? EncryptedName,
        string? LegacyName,
        string? Color);
}
