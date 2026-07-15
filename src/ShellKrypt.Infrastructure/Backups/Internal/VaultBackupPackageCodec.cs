using System.Text.Json;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Backups.Internal;

internal sealed partial class VaultBackupPackageCodec
{
    internal const int CurrentVersion = 2;
    private const int KeySize = 32;
    private const int SaltSize = 16;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 64
    };

    public async Task WriteAsync(VaultSnapshot snapshot, string outputPath, string passphrase, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.BackupExtension, "Encrypted backup");
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var package = await CreateEncryptedPackageAsync(plaintext, passphrase, ct);
        var json = JsonSerializer.Serialize(package, JsonOptions);
        await VaultTransferFileIO.WriteTextAsync(outputPath, json, ct);
    }

    public Task<VaultSnapshot> ReadAsync(string packagePath, string passphrase, CancellationToken ct = default)
        => ReadEncryptedSnapshotAsync(packagePath, passphrase, ct);
}
