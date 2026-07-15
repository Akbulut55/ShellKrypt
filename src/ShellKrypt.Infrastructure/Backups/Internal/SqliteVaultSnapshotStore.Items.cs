using System.Text;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Backups.Internal;

internal sealed partial class SqliteVaultSnapshotStore
{
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

    internal static async Task DeleteItemAsync(SqliteConnection conn, SqliteTransaction tx, string itemId, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM items WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", itemId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    internal static async Task InsertItemAsync(
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
}
