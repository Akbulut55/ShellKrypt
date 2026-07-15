using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Backups.Internal;

namespace ShellKrypt.Infrastructure.DataTransfer.Internal;

internal sealed partial class VaultCsvImportProcessor
{
    private readonly SqliteVaultSnapshotStore _snapshots = new();

    public async Task<VaultCsvImportPreview> PreviewCsvImportAsync(string vaultPath, byte[] vaultKey, string csvPath, CancellationToken ct = default)
    {
        VaultFileGuard.EnsureDifferentPaths(csvPath, vaultPath, "CSV import cannot read from the active vault file.");
        csvPath = VaultFileGuard.EnsureExtension(csvPath, VaultFileGuard.CsvExtension, "CSV import file");
        VaultTransferFileIO.EnsureFileSize(csvPath, VaultTransferLimits.MaxCsvBytes, "CSV import file");
        var snapshot = await _snapshots.CreateAsync(vaultPath, vaultKey, ct);
        var existingKeys = VaultItemDuplicateKey.BuildSet(snapshot);
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
        VaultTransferFileIO.EnsureFileSize(csvPath, VaultTransferLimits.MaxCsvBytes, "CSV import file");
        var snapshot = await _snapshots.CreateAsync(vaultPath, vaultKey, ct);
        var duplicateKeyToId = VaultItemDuplicateKey.BuildMap(snapshot);
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

    private sealed record CsvImportAction(CsvCandidate Candidate, string? DeleteItemId);
}
