using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ShellKrypt.Application.Activity;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Services;

public sealed partial class SqliteActivityLogStore
{
    private static void AppendVaultEntry(ActivityLogEntry entry, byte[] vaultKey)
    {
        using var conn = OpenVaultConnection(entry.VaultPath!);
        EnsureVaultSchema(conn);

        var payload = new ActivityLogPayload(
            entry.Category,
            entry.Title,
            entry.Detail,
            entry.Severity,
            entry.AffectedItem);

        var encryptedPayload = AesGcmBlob.Encrypt(
            vaultKey,
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions)),
            ActivityLogAssociatedData(entry.Id));

        var insert = conn.CreateCommand();
        insert.CommandText = """
        INSERT INTO activity_logs (id, timestampUtc, encryptedPayload)
        VALUES ($id, $timestampUtc, $encryptedPayload);
        """;
        insert.Parameters.AddWithValue("$id", entry.Id);
        insert.Parameters.AddWithValue("$timestampUtc", entry.TimestampUtc);
        insert.Parameters.Add("$encryptedPayload", SqliteType.Blob).Value = encryptedPayload;
        insert.ExecuteNonQuery();

        var prune = conn.CreateCommand();
        prune.CommandText = """
        DELETE FROM activity_logs
        WHERE id NOT IN (
            SELECT id
            FROM activity_logs
            ORDER BY timestampUtc DESC
            LIMIT $limit
        );
        """;
        prune.Parameters.AddWithValue("$limit", MaxEntries);
        prune.ExecuteNonQuery();
    }
}
