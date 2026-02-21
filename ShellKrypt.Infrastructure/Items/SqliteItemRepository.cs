using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed class SqliteItemRepository : IItemRepository
{
    public async Task<IReadOnlyList<VaultItemRow>> ListAsync(string vaultPath, CancellationToken ct = default)
    {
        var list = new List<VaultItemRow>();

        await using var conn = new SqliteConnection($"Data Source={vaultPath};Mode=ReadWrite;");
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT id, type, favorite, createdAtUtc, updatedAtUtc, encryptedPayload
        FROM items
        ORDER BY updatedAtUtc DESC;
        """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetString(0);
            var type = (ItemType)reader.GetInt32(1);
            var fav = reader.GetInt32(2) != 0;
            var created = reader.GetString(3);
            var updated = reader.GetString(4);
            var payload = (byte[])reader["encryptedPayload"];

            var header = new VaultItemHeader(id, type, fav, created, updated);
            list.Add(new VaultItemRow(header, payload));
        }

        return list;
    }

    public async Task InsertAsync(string vaultPath, VaultItemHeader header, byte[] encryptedPayload, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection($"Data Source={vaultPath};Mode=ReadWrite;");
        await conn.OpenAsync(ct);

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
        await using var conn = new SqliteConnection($"Data Source={vaultPath};Mode=ReadWrite;");
        await conn.OpenAsync(ct);

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
        await using var conn = new SqliteConnection($"Data Source={vaultPath};Mode=ReadWrite;");
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}