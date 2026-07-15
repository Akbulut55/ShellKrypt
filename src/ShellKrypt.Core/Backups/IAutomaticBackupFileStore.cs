namespace ShellKrypt.Core.Backups;

public interface IAutomaticBackupFileStore
{
    string BuildBackupPath(string backupDirectory, string vaultPath, DateTimeOffset nowUtc);
    IReadOnlyList<string> EnumerateBackupFiles(string backupDirectory, string vaultPath);
    int ApplyRetention(string backupDirectory, string vaultPath, int retentionCount);
}
