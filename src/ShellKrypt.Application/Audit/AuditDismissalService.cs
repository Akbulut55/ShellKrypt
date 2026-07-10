using ShellKrypt.Application.Ports;

namespace ShellKrypt.Application.Audit;

public sealed class AuditDismissalService
{
    private readonly IAuditDismissalStore _store;

    public AuditDismissalService(IAuditDismissalStore store)
    {
        _store = store;
    }

    public IReadOnlySet<string> LoadFingerprints(string? vaultPath)
    {
        var path = NormalizeVaultPath(vaultPath);
        if (string.IsNullOrWhiteSpace(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return _store.Load()
            .Where(record => string.Equals(record.VaultPath, path, StringComparison.OrdinalIgnoreCase))
            .Select(record => record.Fingerprint)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void Dismiss(string? vaultPath, string fingerprint)
    {
        var path = NormalizeVaultPath(vaultPath);
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(fingerprint))
            return;

        var records = _store.Load().ToList();
        records.RemoveAll(record =>
            string.Equals(record.VaultPath, path, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Fingerprint, fingerprint, StringComparison.OrdinalIgnoreCase));

        records.Add(new DismissedAuditIssueRecord(
            VaultPath: path,
            Fingerprint: fingerprint.Trim(),
            DismissedAtUtc: DateTimeOffset.UtcNow.ToString("O")));

        _store.Save(records
            .OrderBy(record => record.VaultPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.DismissedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private static string NormalizeVaultPath(string? vaultPath)
        => string.IsNullOrWhiteSpace(vaultPath) ? "" : Path.GetFullPath(vaultPath);
}
