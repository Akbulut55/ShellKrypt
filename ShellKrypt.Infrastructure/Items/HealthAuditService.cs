using System.Globalization;
using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class HealthAuditService : IHealthAuditService
{
    private const int OldPasswordDays = 90;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IItemRepository _repo;

    public HealthAuditService(IItemRepository repo)
    {
        _repo = repo;
    }

    public async Task<HealthAuditResult> AnalyzeAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
    {
        var rows = await _repo.ListAsync(vaultPath, vaultKey, ct);
        var entries = new List<WebLoginHealthItem>();
        var issues = new List<HealthAuditIssue>();
        var reusedCount = 0;
        var weakCount = 0;
        var oldCount = 0;

        foreach (var row in rows.Where(row => row.Header.Type == ItemType.Web))
        {
            var payload = JsonSerializer.Deserialize<WebPayload>(
                VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
                JsonOpts);
            if (payload is null)
                continue;

            entries.Add(new WebLoginHealthItem(
                row.Header.Id,
                payload.Title,
                payload.Username,
                payload.Password,
                ParseUpdated(row.Header.UpdatedAtUtc)));
        }

        var reusedGroups = entries
            .Where(x => !string.IsNullOrWhiteSpace(x.Password))
            .GroupBy(x => x.Password)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in reusedGroups)
        {
            reusedCount += group.Count();
            issues.Add(new HealthAuditIssue(
                ItemId: group.First().Id,
                Severity: "High",
                Category: "Reused",
                Title: $"{group.Count()} entries share one password",
                Details: string.Join(", ", group.Select(x => x.Title).Where(t => !string.IsNullOrWhiteSpace(t)).Take(5))));
        }

        foreach (var item in entries)
        {
            var weaknesses = DescribeWeaknesses(item.Password);
            if (!string.IsNullOrWhiteSpace(weaknesses))
            {
                weakCount++;
                issues.Add(new HealthAuditIssue(
                    ItemId: item.Id,
                    Severity: "High",
                    Category: "Weak",
                    Title: item.Title,
                    Details: weaknesses + FormatIdentity(item)));
            }

            var age = DateTimeOffset.UtcNow - item.UpdatedAtUtc;
            if (age.TotalDays >= OldPasswordDays)
            {
                oldCount++;
                issues.Add(new HealthAuditIssue(
                    ItemId: item.Id,
                    Severity: "Medium",
                    Category: "Old",
                    Title: item.Title,
                    Details: $"Last updated {FormatAge(age)} ago" + FormatIdentity(item)));
            }
        }

        return new HealthAuditResult(
            AnalyzedCount: entries.Count,
            ReusedCount: reusedCount,
            WeakCount: weakCount,
            OldCount: oldCount,
            Issues: issues,
            CheckedAtUtc: DateTimeOffset.UtcNow);
    }

    private static DateTimeOffset ParseUpdated(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;

        return DateTimeOffset.UtcNow;
    }

    private static string DescribeWeaknesses(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Empty password.";

        var issues = new List<string>();
        if (password.Length < 12)
            issues.Add($"length {password.Length}");
        if (!password.Any(char.IsLower))
            issues.Add("missing lowercase");
        if (!password.Any(char.IsUpper))
            issues.Add("missing uppercase");
        if (!password.Any(char.IsDigit))
            issues.Add("missing digit");
        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
            issues.Add("missing symbol");

        return issues.Count == 0
            ? ""
            : "Weak: " + string.Join(", ", issues) + ".";
    }

    private static string FormatIdentity(WebLoginHealthItem item)
    {
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.Username))
            pieces.Add($"User: {item.Username}");

        if (pieces.Count == 0)
            return "";

        return " | " + string.Join(" | ", pieces);
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 365)
            return $"{Math.Floor(age.TotalDays / 365)} year(s)";

        if (age.TotalDays >= 30)
            return $"{Math.Floor(age.TotalDays / 30)} month(s)";

        return $"{Math.Max(1, Math.Floor(age.TotalDays))} day(s)";
    }

    private sealed record WebLoginHealthItem(
        string Id,
        string Title,
        string Username,
        string Password,
        DateTimeOffset UpdatedAtUtc);
}
