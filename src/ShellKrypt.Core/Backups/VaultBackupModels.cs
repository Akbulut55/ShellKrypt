using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Core.Backups;

public sealed record VaultSnapshot(
    int Version,
    string ExportedAtUtc,
    IReadOnlyList<VaultSnapshotItem> Items,
    IReadOnlyList<VaultSnapshotLabel> Labels,
    IReadOnlyList<VaultSnapshotItemLabel> ItemLabels);

public sealed record VaultSnapshotItem(
    string Id,
    ItemType Type,
    bool Favorite,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    string PayloadJson);

public sealed record VaultSnapshotLabel(string Id, string Name, string? Color);

public sealed record VaultSnapshotItemLabel(string ItemId, string LabelId);

public sealed record VaultSnapshotSummary(
    int ItemCount,
    int WebCount,
    int CardCount,
    int NoteCount,
    int AuthenticatorCount,
    int ApiKeyCount,
    int ProjectSecretCount,
    int LabelCount,
    int FavoriteCount);

public sealed record VaultEncryptedPackage(
    int Version,
    string ExportedAtUtc,
    VaultKdfParams Kdf,
    string SaltBase64,
    string CiphertextBase64);
