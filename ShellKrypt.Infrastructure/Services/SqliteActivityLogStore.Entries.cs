using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ShellKrypt.Application.Activity;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Services;

public sealed partial class SqliteActivityLogStore
{
    private static IReadOnlyList<ActivityLogEntry> LoadVaultEntries(string vaultPath, byte[] vaultKey)
    {
        var entries = new List<ActivityLogEntry>();

        try
        {
            using var conn = OpenVaultConnection(vaultPath);
            EnsureVaultSchema(conn);

            var cmd = conn.CreateCommand();
            cmd.CommandText = """
            SELECT id, timestampUtc, encryptedPayload
            FROM activity_logs
            ORDER BY timestampUtc DESC
            LIMIT $limit;
            """;
            cmd.Parameters.AddWithValue("$limit", MaxEntries);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                try
                {
                    var id = reader.GetString(0);
                    var timestampUtc = reader.GetString(1);
                    var payloadBytes = reader.GetFieldValue<byte[]>(2);
                    var json = Encoding.UTF8.GetString(AesGcmBlob.Decrypt(vaultKey, payloadBytes, ActivityLogAssociatedData(id)));
                    var payload = JsonSerializer.Deserialize<ActivityLogPayload>(json, JsonOptions);
                    if (payload is null)
                        continue;

                    entries.Add(new ActivityLogEntry(
                        Id: id,
                        TimestampUtc: timestampUtc,
                        Category: payload.Category,
                        Title: payload.Title,
                        Detail: payload.Detail,
                        Severity: payload.Severity,
                        VaultPath: vaultPath)
                    {
                        AffectedItem = payload.AffectedItem
                    });
                }
                catch
                {
                    // Skip individual corrupted entries instead of hiding the whole log.
                }
            }
        }
        catch
        {
            return [];
        }

        return entries;
    }

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

    private static void ClearVaultEntries(string vaultPath)
    {
        using var conn = OpenVaultConnection(vaultPath);
        EnsureVaultSchema(conn);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM activity_logs;";
        cmd.ExecuteNonQuery();
    }
}
