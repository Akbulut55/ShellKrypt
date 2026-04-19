namespace ShellKrypt.Core.Items;

public sealed record HealthAuditIssue(
    string ItemId,
    string Severity,
    string Category,
    string Title,
    string Details);

public sealed record HealthAuditResult(
    int AnalyzedCount,
    int ReusedCount,
    int WeakCount,
    int OldCount,
    IReadOnlyList<HealthAuditIssue> Issues,
    DateTimeOffset CheckedAtUtc);

public interface IHealthAuditService
{
    Task<HealthAuditResult> AnalyzeAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
}
