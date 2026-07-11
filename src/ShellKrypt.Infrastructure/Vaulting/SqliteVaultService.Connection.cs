using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultService
{
    private static SqliteConnection CreateConnection(string vaultPath, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = mode,
            Pooling = false
        };

        return new SqliteConnection(builder.ToString());
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
