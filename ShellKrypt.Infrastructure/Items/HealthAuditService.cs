using System.Text.Json;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService : IHealthAuditService
{
    private const int OldPasswordDays = 90;
    private const int OldApiKeyDays = 180;
    private const int ExpiringCardDays = 90;
    private const int LongClipboardThresholdSeconds = 60;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public HealthAuditService(IItemRepository repo)
    {
        _repo = repo;
    }

    public async Task<HealthAuditResult> AnalyzeAsync(
        string vaultPath,
        byte[] vaultKey,
        HealthAuditOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new HealthAuditOptions();

        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var issues = new List<HealthAuditIssue>();
        var webLogins = new List<WebLoginHealthItem>();
        var apiSecrets = new List<ApiSecretHealthItem>();
        var analyzedCount = 0;

        foreach (var row in rows)
        {
            switch (row.Header.Type)
            {
                case ItemType.Web:
                    analyzedCount++;
                    AddWebLogin(row, vaultKey, webLogins);
                    break;
                case ItemType.Card:
                    analyzedCount++;
                    AddCardFindings(row, vaultKey, issues);
                    break;
                case ItemType.ApiKey:
                    analyzedCount++;
                    AddApiKeyFindings(row, vaultKey, issues, apiSecrets);
                    break;
            }
        }

        AddPasswordFindings(webLogins, issues);
        AddApiSecretReuseFindings(apiSecrets, issues);
        AddSettingsFindings(options, issues);

        var ordered = issues
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Category)
            .ThenBy(issue => issue.AffectedItem, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new HealthAuditResult(
            AnalyzedCount: analyzedCount,
            ReusedCount: ordered.Count(issue => issue.Category is HealthAuditCategory.ReusedPassword),
            WeakCount: ordered.Count(issue => issue.Category is HealthAuditCategory.EmptyPassword or HealthAuditCategory.WeakPassword),
            OldCount: ordered.Count(issue => issue.Category is HealthAuditCategory.StaleCredential or HealthAuditCategory.OldApiKey),
            HighRiskCount: ordered.Count(issue => issue.Severity >= HealthAuditSeverity.High),
            PasswordIssueCount: ordered.Count(IsPasswordIssue),
            CardIssueCount: ordered.Count(issue => issue.ItemType == ItemType.Card),
            ApiKeyIssueCount: ordered.Count(issue => issue.ItemType == ItemType.ApiKey),
            SettingsIssueCount: ordered.Count(issue => issue.ItemType is null),
            Issues: ordered,
            CheckedAtUtc: DateTimeOffset.UtcNow);
    }
}
