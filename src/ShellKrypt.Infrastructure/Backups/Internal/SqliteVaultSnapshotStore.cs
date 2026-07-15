using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Infrastructure.Backups.Internal;

internal sealed partial class SqliteVaultSnapshotStore
{
    internal const int CurrentVersion = 2;
    private readonly IItemRepository _repo = new SqliteItemRepository();

    public Task<VaultSnapshot> CreateAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
        => BuildSnapshotAsync(vaultPath, vaultKey, ct);

    public async Task RestoreAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct = default)
    {
        VaultSnapshotValidator.Validate(snapshot);
        await ImportSnapshotTransactionalAsync(vaultPath, vaultKey, snapshot, ct);
    }

    public static VaultSnapshotSummary Summarize(VaultSnapshot snapshot)
        => SummarizeSnapshot(snapshot);

    private sealed record StoredLabelRow(
        string Id,
        byte[]? EncryptedName,
        string? LegacyName,
        string? Color);
}
