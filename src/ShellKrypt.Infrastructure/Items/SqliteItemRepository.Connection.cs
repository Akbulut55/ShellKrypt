using Microsoft.Data.Sqlite;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class SqliteItemRepository
{
    private static async Task<SqliteConnection> OpenConnectionAsync(string vaultPath, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        var conn = new SqliteConnection(builder.ToString());
        await conn.OpenAsync(ct);
        await ConfigureConnectionAsync(conn, ct);
        return conn;
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection conn, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode=DELETE;
        """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
