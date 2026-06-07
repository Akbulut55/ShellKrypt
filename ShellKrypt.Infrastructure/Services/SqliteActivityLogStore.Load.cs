using System.Text;
using System.Text.Json;
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
}
