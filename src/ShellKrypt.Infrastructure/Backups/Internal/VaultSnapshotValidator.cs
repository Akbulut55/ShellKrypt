namespace ShellKrypt.Infrastructure.Backups.Internal;

internal static class VaultSnapshotValidator
{
    public static void Validate(VaultSnapshot snapshot)
    {
        if (snapshot.Version != SqliteVaultSnapshotStore.CurrentVersion)
            throw new NotSupportedException($"Unsupported snapshot version {snapshot.Version}.");
        if (snapshot.Items.Count > VaultTransferLimits.MaxSnapshotItems)
            throw new InvalidOperationException($"Snapshot contains too many items. Limit: {VaultTransferLimits.MaxSnapshotItems}.");
        if (snapshot.Labels.Count > VaultTransferLimits.MaxSnapshotLabels)
            throw new InvalidOperationException($"Snapshot contains too many labels. Limit: {VaultTransferLimits.MaxSnapshotLabels}.");
        if (snapshot.ItemLabels.Count > VaultTransferLimits.MaxSnapshotItemLabels)
            throw new InvalidOperationException($"Snapshot contains too many item-label links. Limit: {VaultTransferLimits.MaxSnapshotItemLabels}.");

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                throw new InvalidOperationException("Snapshot contains an item without an id.");
            if (!itemIds.Add(item.Id))
                throw new InvalidOperationException("Snapshot contains duplicate item ids.");
            if (item.PayloadJson.Length > VaultTransferLimits.MaxPayloadJsonChars)
                throw new InvalidOperationException("Snapshot contains an item payload that is too large.");
            VaultItemDuplicateKey.ValidatePayload(item.Type, item.PayloadJson);
        }

        var labelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in snapshot.Labels)
        {
            if (string.IsNullOrWhiteSpace(label.Id))
                throw new InvalidOperationException("Snapshot contains a label without an id.");
            if (!labelIds.Add(label.Id))
                throw new InvalidOperationException("Snapshot contains duplicate label ids.");
            if ((label.Name?.Length ?? 0) > VaultTransferLimits.MaxCsvFieldChars)
                throw new InvalidOperationException("Snapshot contains a label name that is too large.");
        }

        var relationships = new HashSet<(string ItemId, string LabelId)>();
        foreach (var relationship in snapshot.ItemLabels)
        {
            if (!itemIds.Contains(relationship.ItemId))
                throw new InvalidOperationException("Snapshot contains an item-label link for an unknown item.");
            if (!labelIds.Contains(relationship.LabelId))
                throw new InvalidOperationException("Snapshot contains an item-label link for an unknown label.");
            if (!relationships.Add((relationship.ItemId, relationship.LabelId)))
                throw new InvalidOperationException("Snapshot contains duplicate item-label links.");
        }
    }
}
