using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Items;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService : IVaultTransferService
{
    private const int PackageVersion = 1;
    private const int KeySize = 32;
    private const int SaltSize = 16;
    private const long MaxEncryptedPackageBytes = 64L * 1024 * 1024;
    private const long MaxCsvBytes = 8L * 1024 * 1024;
    private const int MaxSnapshotJsonBytes = 64 * 1024 * 1024;
    private const int MaxSnapshotItems = 10000;
    private const int MaxSnapshotLabels = 2000;
    private const int MaxSnapshotItemLabels = 50000;
    private const int MaxPayloadJsonChars = 1024 * 1024;
    private const int MaxCsvFieldChars = 16384;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 64
    };

    private readonly IItemRepository _repo = new SqliteItemRepository();

    private sealed record CsvImportAction(CsvCandidate Candidate, string? DeleteItemId);

    private sealed record StoredLabelRow(
        string Id,
        byte[]? EncryptedName,
        string? LegacyName,
        string? Color);
}
