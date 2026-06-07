using System.Text.Json;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    public async Task<VaultSnapshotSummary> GetExportSummaryAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
        => Summarize(await BuildSnapshotAsync(vaultPath, vaultKey, ct));

    public async Task ExportPlaintextJsonAsync(string vaultPath, byte[] vaultKey, string outputPath, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Plaintext export");
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.JsonExtension, "Plaintext export");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(snapshot, JsonOptions), ct);
    }

    public async Task ExportEncryptedAsync(string vaultPath, byte[] vaultKey, string outputPath, string exportPassphrase, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Encrypted backup");
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.BackupExtension, "Encrypted backup");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var package = await CreateEncryptedPackageAsync(snapshotBytes, exportPassphrase, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(package, JsonOptions), ct);
    }
}
