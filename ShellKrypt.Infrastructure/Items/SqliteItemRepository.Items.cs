using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class SqliteItemRepository
{
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
}
