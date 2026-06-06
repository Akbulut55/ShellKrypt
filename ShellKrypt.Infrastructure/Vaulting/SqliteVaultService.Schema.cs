using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultService
{
    private static async Task CreateSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        CREATE TABLE IF NOT EXISTS vault_meta (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            version INTEGER NOT NULL,
            createdAtUtc TEXT NOT NULL,
            kdfMemoryKb INTEGER NOT NULL,
            kdfIterations INTEGER NOT NULL,
            kdfParallelism INTEGER NOT NULL,
            salt BLOB NOT NULL,
            encryptedVaultKey BLOB NOT NULL
        );

        -- created now so Step 3 can start immediately
        CREATE TABLE IF NOT EXISTS items (
            id TEXT PRIMARY KEY,
            type INTEGER NOT NULL,
            favorite INTEGER NOT NULL,
            createdAtUtc TEXT NOT NULL,
            updatedAtUtc TEXT NOT NULL,
            encryptedPayload BLOB NOT NULL
        );

        CREATE TABLE IF NOT EXISTS labels (
            id TEXT PRIMARY KEY,
            encryptedName BLOB,
            name TEXT NOT NULL,
            color TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS item_labels (
            itemId TEXT NOT NULL,
            labelId TEXT NOT NULL,
            PRIMARY KEY (itemId, labelId),
            FOREIGN KEY (itemId) REFERENCES items(id) ON DELETE CASCADE,
            FOREIGN KEY (labelId) REFERENCES labels(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS activity_logs (
            id TEXT PRIMARY KEY,
            timestampUtc TEXT NOT NULL,
            encryptedPayload BLOB NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_labels_name ON labels(name COLLATE NOCASE);
        CREATE INDEX IF NOT EXISTS idx_item_labels_itemId ON item_labels(itemId);
        CREATE INDEX IF NOT EXISTS idx_item_labels_labelId ON item_labels(labelId);
        CREATE INDEX IF NOT EXISTS idx_activity_logs_timestampUtc ON activity_logs(timestampUtc DESC);
        """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
