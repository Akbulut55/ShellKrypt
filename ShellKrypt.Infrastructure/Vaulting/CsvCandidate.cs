using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

internal sealed record CsvCandidate(
    string Id,
    int LineNumber,
    ItemType Type,
    string Title,
    string SecondaryText,
    string PayloadJson,
    string DuplicateKey,
    bool IsValid,
    string? Error,
    string CreatedAtUtc,
    string UpdatedAtUtc)
{
    public static CsvCandidate Invalid(int lineNumber, ItemType type, string title, string error)
        => new(Guid.NewGuid().ToString("N"), lineNumber, type, title, "", "", "", false, error, DateTimeOffset.UtcNow.ToString("O"), DateTimeOffset.UtcNow.ToString("O"));

    public VaultCsvImportRowPreview ToPreview(VaultCsvRowStatus status, string? message)
        => new(LineNumber, Type, Title, SecondaryText, status, message);
}
