using System.Text.Json;
using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Infrastructure.Backups.Internal;
using ShellKrypt.Infrastructure.Services;

namespace ShellKrypt.Infrastructure.DataTransfer;

public sealed class VaultPlaintextExportService : IVaultPlaintextExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 64
    };
    private readonly SqliteVaultSnapshotStore _snapshots = new();

    public async Task ExportJsonAsync(string vaultPath, byte[] vaultKey, string outputPath, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Plaintext export");
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.JsonExtension, "Plaintext export");
        var snapshot = await _snapshots.CreateAsync(vaultPath, vaultKey, ct);
        await VaultTransferFileIO.WriteTextAsync(outputPath, JsonSerializer.Serialize(snapshot, JsonOptions), ct);
    }
}
