namespace ShellKrypt.Infrastructure.Services;

public sealed partial class SqliteActivityLogStore
{
    private static void ClearVaultEntries(string vaultPath)
    {
        using var conn = OpenVaultConnection(vaultPath);
        EnsureVaultSchema(conn);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM activity_logs;";
        cmd.ExecuteNonQuery();
    }
}
