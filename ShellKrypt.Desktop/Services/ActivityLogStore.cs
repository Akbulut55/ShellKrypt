using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Desktop.Services;

public sealed partial class ActivityLogStore
{
    private const int MaxEntries = 400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<ActivityLogEntry> Load(string? vaultPath = null, byte[]? vaultKey = null)
    {
        if (!string.IsNullOrWhiteSpace(vaultPath) && vaultKey is { Length: > 0 })
            return LoadVaultEntries(vaultPath, vaultKey);

        return [];
    }

    public void Append(ActivityLogEntry entry, byte[]? vaultKey = null)
    {
        entry = SanitizeEntry(entry);

        if (!string.IsNullOrWhiteSpace(entry.VaultPath) && vaultKey is { Length: > 0 })
        {
            AppendVaultEntry(entry, vaultKey);
            return;
        }

        // Activity logs are vault-scoped and encrypted. Events without a vault key are intentionally not persisted.
    }

    public void Clear(string? vaultPath = null, byte[]? vaultKey = null)
    {
        if (!string.IsNullOrWhiteSpace(vaultPath) && vaultKey is { Length: > 0 })
        {
            ClearVaultEntries(vaultPath);
            return;
        }

        // Legacy global activity logs are quarantined by leaving them unread and unwritten.
    }

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

        var encryptedPayload = AesGcmBlob.Encrypt(vaultKey, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions)), ActivityLogAssociatedData(entry.Id));

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

    private static SqliteConnection OpenVaultConnection(string vaultPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        var conn = new SqliteConnection(builder.ToString());
        conn.Open();

        var pragmas = conn.CreateCommand();
        pragmas.CommandText = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode=DELETE;
        """;
        pragmas.ExecuteNonQuery();

        return conn;
    }

    private static void EnsureVaultSchema(SqliteConnection conn)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS activity_logs (
            id TEXT PRIMARY KEY,
            timestampUtc TEXT NOT NULL,
            encryptedPayload BLOB NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_activity_logs_timestampUtc
        ON activity_logs(timestampUtc DESC);
        """;
        cmd.ExecuteNonQuery();
    }

    private static string NormalizePath(string? vaultPath)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
            return "";

        try
        {
            return Path.GetFullPath(vaultPath.Trim());
        }
        catch
        {
            return vaultPath.Trim();
        }
    }

    private static byte[] ActivityLogAssociatedData(string id)
        => AesGcmBlob.CreateAssociatedData("activity-log", "v1", id);

    private static ActivityLogEntry SanitizeEntry(ActivityLogEntry entry)
        => entry with
        {
            Detail = SanitizeLogText(entry.Detail),
            AffectedItem = string.IsNullOrWhiteSpace(entry.AffectedItem) ? entry.AffectedItem : SanitizeLogText(entry.AffectedItem)
        };

    private static string SanitizeLogText(string value)
    {
        var sanitized = SensitiveAssignmentRegex().Replace(value ?? string.Empty, match => $"{match.Groups[1].Value}=[redacted]");
        return CardLikeNumberRegex().Replace(sanitized, "[redacted-number]");
    }

    [GeneratedRegex(@"\b(password|passphrase|secret|token|api[-_ ]?key|cvc|cvv)\s*[:=]\s*[^,\s;]+", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveAssignmentRegex();

    [GeneratedRegex(@"\b(?:\d[ -]?){12,19}\b")]
    private static partial Regex CardLikeNumberRegex();

    private sealed record ActivityLogPayload(
        string Category,
        string Title,
        string Detail,
        string Severity,
        string? AffectedItem);
}
