using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    public async Task<VaultSnapshotSummary> GetEncryptedImportSummaryAsync(string packagePath, string exportPassphrase, CancellationToken ct = default)
        => Summarize(await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct));

    public async Task ImportEncryptedAsync(string packagePath, string exportPassphrase, string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(packagePath, vaultPath, "Encrypted backup import cannot read from the active vault file.");
        var snapshot = await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct);
        await ImportSnapshotAsync(vaultPath, vaultKey, snapshot, ct);
    }

    public async Task ImportSnapshotAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct = default)
    {
        ValidateSnapshot(snapshot);
        await ImportSnapshotTransactionalAsync(vaultPath, vaultKey, snapshot, ct);
    }
}
