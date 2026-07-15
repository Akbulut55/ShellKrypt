using ShellKrypt.Core.Backups;
using ShellKrypt.Infrastructure.Backups.Internal;

namespace ShellKrypt.Infrastructure.Backups;

public sealed class EncryptedVaultBackupService : IEncryptedVaultBackupService
{
    private readonly SqliteVaultSnapshotStore _snapshots = new();
    private readonly VaultBackupPackageCodec _packages = new();

    public async Task<VaultSnapshotSummary> GetSummaryAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
        => SqliteVaultSnapshotStore.Summarize(await _snapshots.CreateAsync(vaultPath, vaultKey, ct));

    public async Task CreateAsync(string vaultPath, byte[] vaultKey, string outputPath, string backupPassphrase, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Encrypted backup");
        var snapshot = await _snapshots.CreateAsync(vaultPath, vaultKey, ct);
        await _packages.WriteAsync(snapshot, outputPath, backupPassphrase, ct);
    }

    public async Task<VaultSnapshotSummary> InspectAsync(string packagePath, string backupPassphrase, CancellationToken ct = default)
        => SqliteVaultSnapshotStore.Summarize(await _packages.ReadAsync(packagePath, backupPassphrase, ct));

    public async Task RestoreAsync(string packagePath, string backupPassphrase, string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(packagePath, vaultPath, "Encrypted backup import cannot read from the active vault file.");
        var snapshot = await _packages.ReadAsync(packagePath, backupPassphrase, ct);
        await _snapshots.RestoreAsync(vaultPath, vaultKey, snapshot, ct);
    }
}
