using ShellKrypt.Core.Items;

namespace ShellKrypt.Core.Vaulting;

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

public sealed record VaultSnapshotLabel(
    string Id,
    string Name,
    string? Color);

public sealed record VaultSnapshotItemLabel(
    string ItemId,
    string LabelId);

public sealed record VaultSnapshotSummary(
    int ItemCount,
    int WebCount,
    int CardCount,
    int NoteCount,
    int AuthenticatorCount,
    int LabelCount,
    int FavoriteCount);

public sealed record VaultEncryptedPackage(
    int Version,
    string ExportedAtUtc,
    VaultKdfParams Kdf,
    string SaltBase64,
    string CiphertextBase64);

public enum VaultCsvDuplicateStrategy
{
    SkipDuplicates = 1,
    OverwriteDuplicates = 2,
    ImportAll = 3
}

public enum VaultCsvRowStatus
{
    New = 1,
    Duplicate = 2,
    Invalid = 3
}

public sealed record VaultCsvImportPreview(
    int TotalRows,
    int NewRows,
    int DuplicateRows,
    int InvalidRows,
    IReadOnlyList<VaultCsvImportRowPreview> Rows);

public sealed record VaultCsvImportRowPreview(
    int LineNumber,
    ItemType Type,
    string Title,
    string SecondaryText,
    VaultCsvRowStatus Status,
    string? Message);
