using Microsoft.Data.Sqlite;

namespace ShellKrypt.Infrastructure.Services;

public sealed partial class SqliteActivityLogStore
{
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
}
