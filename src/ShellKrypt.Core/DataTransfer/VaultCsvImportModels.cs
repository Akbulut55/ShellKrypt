using ShellKrypt.Core.Items;

namespace ShellKrypt.Core.DataTransfer;

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
