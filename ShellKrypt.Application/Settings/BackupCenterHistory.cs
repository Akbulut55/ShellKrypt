namespace ShellKrypt.Application.Settings;

public sealed class BackupCenterHistory
{
    public const int MaxRecentEntries = 10;

    public string LastEncryptedBackupPath { get; set; } = "";
    public string LastVerifiedBackupPath { get; set; } = "";
    public string LastRestoredBackupPath { get; set; } = "";
    public string LastPlaintextExportPath { get; set; } = "";
    public string LastCsvImportPath { get; set; } = "";
    public List<BackupCenterHistoryEntry> RecentEntries { get; set; } = [];

    public void AddEntry(BackupCenterHistoryEntry entry)
    {
        entry.Normalize();
        if (string.IsNullOrWhiteSpace(entry.Operation) || string.IsNullOrWhiteSpace(entry.TimestampUtc))
            return;

        RecentEntries.Insert(0, entry);
        Normalize();
    }

    public void Normalize()
    {
        LastEncryptedBackupPath = NormalizePath(LastEncryptedBackupPath);
        LastVerifiedBackupPath = NormalizePath(LastVerifiedBackupPath);
        LastRestoredBackupPath = NormalizePath(LastRestoredBackupPath);
        LastPlaintextExportPath = NormalizePath(LastPlaintextExportPath);
        LastCsvImportPath = NormalizePath(LastCsvImportPath);

        RecentEntries ??= [];
        foreach (var entry in RecentEntries)
            entry.Normalize();

        RecentEntries = RecentEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Operation) && !string.IsNullOrWhiteSpace(entry.TimestampUtc))
            .OrderByDescending(entry => ParseTimestamp(entry.TimestampUtc))
            .Take(MaxRecentEntries)
            .ToList();
    }

    private static string NormalizePath(string? path) => string.IsNullOrWhiteSpace(path) ? "" : path.Trim();

    private static DateTimeOffset ParseTimestamp(string timestampUtc)
        => DateTimeOffset.TryParse(timestampUtc, out var parsed) ? parsed : DateTimeOffset.MinValue;
}

public sealed class BackupCenterHistoryEntry
{
    public string Operation { get; set; } = "";
    public string Status { get; set; } = "";
    public string TimestampUtc { get; set; } = "";
    public string VaultName { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public int ItemCount { get; set; }
    public int LabelCount { get; set; }

    public void Normalize()
    {
        Operation = NormalizeText(Operation).ToLowerInvariant();
        Status = NormalizeText(Status).ToLowerInvariant();
        TimestampUtc = NormalizeText(TimestampUtc);
        VaultName = NormalizeText(VaultName);
        FileName = NormalizeText(FileName);
        FullPath = NormalizeText(FullPath);
        ItemCount = Math.Max(0, ItemCount);
        LabelCount = Math.Max(0, LabelCount);
    }

    private static string NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
}
