namespace ShellKrypt.Core.Backups;

public interface IEncryptedVaultBackupService
{
    Task<VaultSnapshotSummary> GetSummaryAsync(
        string vaultPath,
        byte[] vaultKey,
        CancellationToken ct = default);

    Task CreateAsync(
        string vaultPath,
        byte[] vaultKey,
        string outputPath,
        string backupPassphrase,
        CancellationToken ct = default);

    Task<VaultSnapshotSummary> InspectAsync(
        string packagePath,
        string backupPassphrase,
        CancellationToken ct = default);

    Task RestoreAsync(
        string packagePath,
        string backupPassphrase,
        string vaultPath,
        byte[] vaultKey,
        CancellationToken ct = default);
}
