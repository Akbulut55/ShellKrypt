using Microsoft.Data.Sqlite;

namespace ShellKrypt.Infrastructure.Backups.Internal;

internal sealed partial class SqliteVaultSnapshotStore
{
    internal static async Task<SqliteConnection> OpenVaultConnectionAsync(string vaultPath, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        var conn = new SqliteConnection(builder.ToString());
        await conn.OpenAsync(ct);
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode=DELETE;
        """;
        await cmd.ExecuteNonQueryAsync(ct);
        return conn;
    }
}
