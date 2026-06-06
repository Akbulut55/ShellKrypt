namespace ShellKrypt.Core.Items;

public enum HealthAuditSeverity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum HealthAuditCategory
{
    EmptyPassword,
    WeakPassword,
    ReusedPassword,
    StaleCredential,
    ExpiredCard,
    ExpiringCard,
    ReusedApiSecret,
    OldApiKey,
    ApiKeyMissingSecret,
    AutoLockDisabled,
    FocusLockDisabled,
    ClipboardTimeoutLong,
    ClipboardCopyEnabled
}

public enum HealthAuditRecommendedAction
{
    None,
    OpenWebLogin,
    GenerateReplacementPassword,
    OpenCard,
    OpenApiKey,
    OpenSettings
}

public sealed record HealthAuditOptions(
    bool AutoLockEnabled = true,
    bool LockOnDeactivate = true,
    int ClipboardClearSeconds = 15,
    bool ClipboardCopyEnabled = false);

public sealed record HealthAuditIssue(
    string Fingerprint,
    string ItemId,
    ItemType? ItemType,
    HealthAuditSeverity Severity,
    HealthAuditCategory Category,
    string AffectedItem,
    string Title,
    string Details,
    HealthAuditRecommendedAction RecommendedAction);

public sealed record HealthAuditResult(
    int AnalyzedCount,
    int ReusedCount,
    int WeakCount,
    int OldCount,
    int HighRiskCount,
    int PasswordIssueCount,
    int CardIssueCount,
    int ApiKeyIssueCount,
    int SettingsIssueCount,
    IReadOnlyList<HealthAuditIssue> Issues,
    DateTimeOffset CheckedAtUtc);

public interface IHealthAuditService
{
    Task<HealthAuditResult> AnalyzeAsync(
        string vaultPath,
        byte[] vaultKey,
        HealthAuditOptions? options = null,
        CancellationToken ct = default);
}
