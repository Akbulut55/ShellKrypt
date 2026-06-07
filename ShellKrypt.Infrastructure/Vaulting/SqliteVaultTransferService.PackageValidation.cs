using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    private static void ValidatePackageMetadata(VaultEncryptedPackage package)
    {
        if (package.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported package version {package.Version}.");

        if (!VaultKdfPolicy.IsValidStored(package.Kdf, out var kdfError))
            throw new InvalidOperationException(kdfError);
    }

    private static void ValidateSnapshot(VaultSnapshot snapshot)
    {
        if (snapshot.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported snapshot version {snapshot.Version}.");

        if (snapshot.Items.Count > MaxSnapshotItems)
            throw new InvalidOperationException($"Snapshot contains too many items. Limit: {MaxSnapshotItems}.");

        if (snapshot.Labels.Count > MaxSnapshotLabels)
            throw new InvalidOperationException($"Snapshot contains too many labels. Limit: {MaxSnapshotLabels}.");

        if (snapshot.ItemLabels.Count > MaxSnapshotItemLabels)
            throw new InvalidOperationException($"Snapshot contains too many item-label links. Limit: {MaxSnapshotItemLabels}.");

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                throw new InvalidOperationException("Snapshot contains an item without an id.");

            if (!itemIds.Add(item.Id))
                throw new InvalidOperationException("Snapshot contains duplicate item ids.");

            if (item.PayloadJson.Length > MaxPayloadJsonChars)
                throw new InvalidOperationException("Snapshot contains an item payload that is too large.");

            _ = BuildDuplicateKey(item.Type, item.PayloadJson);
        }

        var labelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in snapshot.Labels)
        {
            if (string.IsNullOrWhiteSpace(label.Id))
                throw new InvalidOperationException("Snapshot contains a label without an id.");

            if (!labelIds.Add(label.Id))
                throw new InvalidOperationException("Snapshot contains duplicate label ids.");

            if ((label.Name?.Length ?? 0) > MaxCsvFieldChars)
                throw new InvalidOperationException("Snapshot contains a label name that is too large.");
        }
    }
}
