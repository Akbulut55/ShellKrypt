using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
    private static void AddIssue(
        List<HealthAuditIssue> issues,
        string itemId,
        ItemType? itemType,
        HealthAuditSeverity severity,
        HealthAuditCategory category,
        string affectedItem,
        string title,
        string details,
        HealthAuditRecommendedAction action)
    {
        var issue = new HealthAuditIssue(
            Fingerprint: BuildFingerprint(itemId, itemType, severity, category, title, details),
            ItemId: itemId,
            ItemType: itemType,
            Severity: severity,
            Category: category,
            AffectedItem: SafeName(affectedItem, itemType is null ? "Settings" : "Vault item"),
            Title: title,
            Details: details,
            RecommendedAction: action);

        issues.Add(issue);
    }

    private static string BuildFingerprint(
        string itemId,
        ItemType? itemType,
        HealthAuditSeverity severity,
        HealthAuditCategory category,
        string title,
        string details)
    {
        var raw = string.Join("|",
            itemType?.ToString() ?? "Settings",
            itemId.Trim(),
            severity,
            category,
            title.Trim(),
            details.Trim());

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static bool IsPasswordIssue(HealthAuditIssue issue)
        => issue.Category is HealthAuditCategory.EmptyPassword
            or HealthAuditCategory.WeakPassword
            or HealthAuditCategory.ReusedPassword
            or HealthAuditCategory.StaleCredential;

    private static DateTimeOffset ParseUpdated(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;

        return DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset? GetCardExpiryEnd(int month, int year)
    {
        if (month is < 1 or > 12)
            return null;

        if (year is > 0 and < 100)
            year += 2000;

        if (year < 1900)
            return null;

        var lastDay = DateTime.DaysInMonth(year, month);
        return new DateTimeOffset(year, month, lastDay, 23, 59, 59, TimeSpan.Zero);
    }

    private static string FormatExpiry(int month, int year)
    {
        if (year is > 0 and < 100)
            year += 2000;

        return month is >= 1 and <= 12 && year > 0
            ? $"{month:00}/{year % 100:00}"
            : "the saved expiry date";
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age.TotalDays >= 365)
            return $"{Math.Floor(age.TotalDays / 365)} year(s)";

        if (age.TotalDays >= 30)
            return $"{Math.Floor(age.TotalDays / 30)} month(s)";

        return $"{Math.Max(1, Math.Floor(age.TotalDays))} day(s)";
    }

    private static string SafeName(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
