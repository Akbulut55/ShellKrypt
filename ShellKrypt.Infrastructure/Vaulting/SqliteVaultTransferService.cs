using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;
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

    public async Task<VaultSnapshotSummary> GetExportSummaryAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
        => Summarize(await BuildSnapshotAsync(vaultPath, vaultKey, ct));

    public async Task ExportPlaintextJsonAsync(string vaultPath, byte[] vaultKey, string outputPath, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Plaintext export");
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.JsonExtension, "Plaintext export");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(snapshot, JsonOptions), ct);
    }

    public async Task ExportEncryptedAsync(string vaultPath, byte[] vaultKey, string outputPath, string exportPassphrase, CancellationToken ct = default)
    {
        outputPath = VaultFileGuard.EnsureNotActiveVaultTarget(vaultPath, outputPath, "Encrypted backup");
        outputPath = VaultFileGuard.EnsureExtension(outputPath, VaultFileGuard.BackupExtension, "Encrypted backup");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var snapshotBytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions);
        var package = await CreateEncryptedPackageAsync(snapshotBytes, exportPassphrase, ct);
        await WriteTextAsync(outputPath, JsonSerializer.Serialize(package, JsonOptions), ct);
    }

    public async Task<VaultSnapshotSummary> GetEncryptedImportSummaryAsync(string packagePath, string exportPassphrase, CancellationToken ct = default)
        => Summarize(await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct));

    public async Task ImportEncryptedAsync(string packagePath, string exportPassphrase, string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(packagePath, vaultPath, "Encrypted backup import cannot read from the active vault file.");
        var snapshot = await ReadEncryptedSnapshotAsync(packagePath, exportPassphrase, ct);
        await ImportSnapshotAsync(vaultPath, vaultKey, snapshot, ct);
    }

    public async Task ImportSnapshotAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct = default)
    {
        ValidateSnapshot(snapshot);
        await ImportSnapshotTransactionalAsync(vaultPath, vaultKey, snapshot, ct);
    }

    public async Task<VaultCsvImportPreview> PreviewCsvImportAsync(string vaultPath, byte[] vaultKey, string csvPath, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(csvPath, vaultPath, "CSV import cannot read from the active vault file.");
        csvPath = VaultFileGuard.EnsureExtension(csvPath, VaultFileGuard.CsvExtension, "CSV import file");
        EnsureFileSize(csvPath, MaxCsvBytes, "CSV import file");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var existingKeys = BuildDuplicateKeySet(snapshot);
        var csvText = await File.ReadAllTextAsync(csvPath, ct);
        var candidates = VaultCsvImportParser.ParseCandidates(csvText);

        var seenKeys = new HashSet<string>(existingKeys, StringComparer.Ordinal);
        var rows = new List<VaultCsvImportRowPreview>();
        var newRows = 0;
        var duplicateRows = 0;
        var invalidRows = 0;

        foreach (var candidate in candidates)
        {
            if (!candidate.IsValid)
            {
                rows.Add(candidate.ToPreview(VaultCsvRowStatus.Invalid, candidate.Error ?? "Invalid row."));
                invalidRows++;
                continue;
            }

            if (!seenKeys.Add(candidate.DuplicateKey))
            {
                rows.Add(candidate.ToPreview(VaultCsvRowStatus.Duplicate, "Duplicate item."));
                duplicateRows++;
                continue;
            }

            rows.Add(candidate.ToPreview(VaultCsvRowStatus.New, null));
            newRows++;
        }

        return new VaultCsvImportPreview(candidates.Count, newRows, duplicateRows, invalidRows, rows);
    }

    public async Task ImportCsvAsync(string vaultPath, byte[] vaultKey, string csvPath, VaultCsvDuplicateStrategy strategy, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(csvPath, vaultPath, "CSV import cannot read from the active vault file.");
        csvPath = VaultFileGuard.EnsureExtension(csvPath, VaultFileGuard.CsvExtension, "CSV import file");
        EnsureFileSize(csvPath, MaxCsvBytes, "CSV import file");
        var snapshot = await BuildSnapshotAsync(vaultPath, vaultKey, ct);
        var duplicateKeyToId = BuildDuplicateKeyMap(snapshot);
        var csvText = await File.ReadAllTextAsync(csvPath, ct);
        var candidates = VaultCsvImportParser.ParseCandidates(csvText);

        var seenKeys = new HashSet<string>(duplicateKeyToId.Keys, StringComparer.Ordinal);
        var actions = new List<CsvImportAction>();

        foreach (var candidate in candidates)
        {
            if (!candidate.IsValid)
                continue;

            var duplicateDetected = seenKeys.Contains(candidate.DuplicateKey);
            if (duplicateDetected && strategy == VaultCsvDuplicateStrategy.SkipDuplicates)
                continue;

            string? deleteItemId = null;
            if (duplicateDetected && strategy == VaultCsvDuplicateStrategy.OverwriteDuplicates && duplicateKeyToId.TryGetValue(candidate.DuplicateKey, out var existingId))
            {
                deleteItemId = existingId;
                duplicateKeyToId.Remove(candidate.DuplicateKey);
                seenKeys.Remove(candidate.DuplicateKey);
            }

            actions.Add(new CsvImportAction(candidate, deleteItemId));
            duplicateKeyToId[candidate.DuplicateKey] = candidate.Id;
            seenKeys.Add(candidate.DuplicateKey);
        }

        await ImportCsvTransactionalAsync(vaultPath, vaultKey, actions, ct);
    }

    private readonly IItemRepository _repo = new SqliteItemRepository();

    private sealed record CsvImportAction(CsvCandidate Candidate, string? DeleteItemId);

    private sealed record StoredLabelRow(
        string Id,
        byte[]? EncryptedName,
        string? LegacyName,
        string? Color);
}
