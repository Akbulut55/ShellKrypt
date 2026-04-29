using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShellKrypt.Desktop.Services;

public sealed record DismissedAuditIssueRecord(
    string VaultPath,
    string Fingerprint,
    string DismissedAtUtc);

public sealed class DismissedAuditIssueStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlySet<string> LoadFingerprints(string? vaultPath)
    {
        var path = NormalizeVaultPath(vaultPath);
        if (string.IsNullOrWhiteSpace(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return LoadRecords()
            .Where(record => string.Equals(record.VaultPath, path, StringComparison.OrdinalIgnoreCase))
            .Select(record => record.Fingerprint)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void Dismiss(string? vaultPath, string fingerprint)
    {
        var path = NormalizeVaultPath(vaultPath);
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(fingerprint))
            return;

        var records = LoadRecords();
        records.RemoveAll(record =>
            string.Equals(record.VaultPath, path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));

        records.Add(new DismissedAuditIssueRecord(
            VaultPath: path,
            Fingerprint: fingerprint.Trim(),
            DismissedAtUtc: DateTimeOffset.UtcNow.ToString("O")));

        SaveRecords(records);
    }

    private static List<DismissedAuditIssueRecord> LoadRecords()
    {
        try
        {
            if (!File.Exists(DefaultPaths.AuditDismissalsPath))
                return new List<DismissedAuditIssueRecord>();

            return JsonSerializer.Deserialize<List<DismissedAuditIssueRecord>>(
                       File.ReadAllText(DefaultPaths.AuditDismissalsPath),
                       JsonOptions)
                   ?? new List<DismissedAuditIssueRecord>();
        }
        catch
        {
            return new List<DismissedAuditIssueRecord>();
        }
    }

    private static void SaveRecords(List<DismissedAuditIssueRecord> records)
    {
        var dir = Path.GetDirectoryName(DefaultPaths.AuditDismissalsPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(records
            .OrderBy(record => record.VaultPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.DismissedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToList(), JsonOptions);

        File.WriteAllText(DefaultPaths.AuditDismissalsPath, json);
    }

    private static string NormalizeVaultPath(string? vaultPath)
        => string.IsNullOrWhiteSpace(vaultPath) ? "" : Path.GetFullPath(vaultPath);
}
