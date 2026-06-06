using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed class HealthAuditService : IHealthAuditService
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

    private static void AddWebLogin(VaultItemRow row, byte[] vaultKey, List<WebLoginHealthItem> webLogins)
    {
        var payload = JsonSerializer.Deserialize<WebPayload>(
            VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
            JsonOpts);
        if (payload is null)
            return;

        webLogins.Add(new WebLoginHealthItem(
            row.Header.Id,
            SafeName(payload.Title, "Web login"),
            payload.Username,
            payload.Password,
            ParseUpdated(row.Header.UpdatedAtUtc)));
    }

    private static void AddPasswordFindings(IReadOnlyList<WebLoginHealthItem> webLogins, List<HealthAuditIssue> issues)
    {
        var reusedPasswordIds = new HashSet<string>(StringComparer.Ordinal);
        var reusedGroups = webLogins
            .Where(item => !string.IsNullOrWhiteSpace(item.Password))
            .GroupBy(item => item.Password, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);

        foreach (var group in reusedGroups)
        {
            var affected = group.ToList();
            foreach (var item in affected)
            {
                reusedPasswordIds.Add(item.Id);
                AddIssue(
                    issues,
                    item.Id,
                    ItemType.Web,
                    HealthAuditSeverity.High,
                    HealthAuditCategory.ReusedPassword,
                    item.Title,
                    "Reused password",
                    $"This login shares its saved password with {affected.Count - 1} other web login(s). Use a unique generated password for this account.",
                    HealthAuditRecommendedAction.GenerateReplacementPassword);
            }
        }

        foreach (var item in webLogins)
        {
            if (string.IsNullOrWhiteSpace(item.Password))
            {
                AddIssue(
                    issues,
                    item.Id,
                    ItemType.Web,
                    HealthAuditSeverity.High,
                    HealthAuditCategory.EmptyPassword,
                    item.Title,
                    "Empty password",
                    "This login does not have a saved password. Add a unique password or remove the incomplete record.",
                    HealthAuditRecommendedAction.GenerateReplacementPassword);
            }
            else
            {
                var weaknesses = DescribeWeaknesses(item.Password);
                if (!string.IsNullOrWhiteSpace(weaknesses))
                {
                    AddIssue(
                        issues,
                        item.Id,
                        ItemType.Web,
                        reusedPasswordIds.Contains(item.Id) ? HealthAuditSeverity.High : HealthAuditSeverity.Medium,
                        HealthAuditCategory.WeakPassword,
                        item.Title,
                        "Weak password",
                        weaknesses,
                        HealthAuditRecommendedAction.GenerateReplacementPassword);
                }
            }

            var age = DateTimeOffset.UtcNow - item.UpdatedAtUtc;
            if (age.TotalDays >= OldPasswordDays)
            {
                AddIssue(
                    issues,
                    item.Id,
                    ItemType.Web,
                    HealthAuditSeverity.Low,
                    HealthAuditCategory.StaleCredential,
                    item.Title,
                    "Password not updated recently",
                    $"This login was last updated {FormatAge(age)} ago. Review whether the credential should be rotated.",
                    HealthAuditRecommendedAction.OpenWebLogin);
            }
        }
    }

    private static void AddCardFindings(VaultItemRow row, byte[] vaultKey, List<HealthAuditIssue> issues)
    {
        var payload = JsonSerializer.Deserialize<CardPayload>(
            VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
            JsonOpts);
        if (payload is null)
            return;

        var title = SafeName(payload.Title, "Credit card");
        var expiry = GetCardExpiryEnd(payload.ExpiryMonth, payload.ExpiryYear);
        if (expiry is null)
            return;

        var now = DateTimeOffset.UtcNow;
        if (expiry < now)
        {
            AddIssue(
                issues,
                row.Header.Id,
                ItemType.Card,
                HealthAuditSeverity.Medium,
                HealthAuditCategory.ExpiredCard,
                title,
                "Expired card",
                $"This card expired in {FormatExpiry(payload.ExpiryMonth, payload.ExpiryYear)}. Update or remove it if it is no longer valid.",
                HealthAuditRecommendedAction.OpenCard);
            return;
        }

        if (expiry <= now.AddDays(ExpiringCardDays))
        {
            AddIssue(
                issues,
                row.Header.Id,
                ItemType.Card,
                HealthAuditSeverity.Low,
                HealthAuditCategory.ExpiringCard,
                title,
                "Card expiring soon",
                $"This card expires in {FormatExpiry(payload.ExpiryMonth, payload.ExpiryYear)}. Prepare a replacement if this card is still in use.",
                HealthAuditRecommendedAction.OpenCard);
        }
    }

    private static void AddApiKeyFindings(
        VaultItemRow row,
        byte[] vaultKey,
        List<HealthAuditIssue> issues,
        List<ApiSecretHealthItem> apiSecrets)
    {
        var payload = JsonSerializer.Deserialize<ApiKeyPayload>(
            VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
            JsonOpts);
        if (payload is null)
            return;

        var name = SafeName(payload.Name, "API key");
        var sensitiveFields = payload.Fields
            .Where(field => field.IsSensitive && !string.IsNullOrWhiteSpace(field.Value))
            .ToList();

        foreach (var field in sensitiveFields)
        {
            apiSecrets.Add(new ApiSecretHealthItem(
                row.Header.Id,
                name,
                field.Value.Trim()));
        }

        if (sensitiveFields.Count == 0)
        {
            AddIssue(
                issues,
                row.Header.Id,
                ItemType.ApiKey,
                HealthAuditSeverity.Low,
                HealthAuditCategory.ApiKeyMissingSecret,
                name,
                "No sensitive API fields",
                "This API key record has no populated sensitive fields. Review whether it is metadata-only or incomplete.",
                HealthAuditRecommendedAction.OpenApiKey);
        }

        var age = DateTimeOffset.UtcNow - ParseUpdated(row.Header.UpdatedAtUtc);
        if (age.TotalDays >= OldApiKeyDays)
        {
            AddIssue(
                issues,
                row.Header.Id,
                ItemType.ApiKey,
                HealthAuditSeverity.Medium,
                HealthAuditCategory.OldApiKey,
                name,
                "API key not rotated recently",
                $"This API key was last updated {FormatAge(age)} ago. Review whether it should be rotated.",
                HealthAuditRecommendedAction.OpenApiKey);
        }
    }

    private static void AddApiSecretReuseFindings(IReadOnlyList<ApiSecretHealthItem> apiSecrets, List<HealthAuditIssue> issues)
    {
        var reusedGroups = apiSecrets
            .GroupBy(secret => secret.SecretValue, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);

        foreach (var group in reusedGroups)
        {
            var affected = group
                .GroupBy(secret => secret.ItemId, StringComparer.Ordinal)
                .Select(grouped => grouped.First())
                .ToList();

            foreach (var item in affected)
            {
                AddIssue(
                    issues,
                    item.ItemId,
                    ItemType.ApiKey,
                    HealthAuditSeverity.High,
                    HealthAuditCategory.ReusedApiSecret,
                    item.Name,
                    "Reused API secret",
                    "A sensitive field in this API key matches another sensitive API key field. Rotate duplicated secrets where possible.",
                    HealthAuditRecommendedAction.OpenApiKey);
            }
        }
    }

    private static void AddSettingsFindings(HealthAuditOptions options, List<HealthAuditIssue> issues)
    {
        if (!options.AutoLockEnabled)
        {
            AddIssue(
                issues,
                "settings:autolock",
                null,
                HealthAuditSeverity.Medium,
                HealthAuditCategory.AutoLockDisabled,
                "Security Settings",
                "Auto-lock is disabled",
                "Enable auto-lock so unlocked vault sessions close after inactivity.",
                HealthAuditRecommendedAction.OpenSettings);
        }

        if (!options.LockOnDeactivate)
        {
            AddIssue(
                issues,
                "settings:focus-lock",
                null,
                HealthAuditSeverity.Low,
                HealthAuditCategory.FocusLockDisabled,
                "Security Settings",
                "Focus-loss lock is disabled",
                "Enable lock on app deactivate if you want the vault to lock when ShellKrypt loses focus.",
                HealthAuditRecommendedAction.OpenSettings);
        }

        if (options.ClipboardClearSeconds > LongClipboardThresholdSeconds)
        {
            AddIssue(
                issues,
                "settings:clipboard-timeout",
                null,
                HealthAuditSeverity.Low,
                HealthAuditCategory.ClipboardTimeoutLong,
                "Clipboard Settings",
                "Clipboard clear timeout is long",
                $"Copied secrets are kept for {options.ClipboardClearSeconds} seconds before best-effort clearing. Consider a shorter timeout.",
                HealthAuditRecommendedAction.OpenSettings);
        }

        if (options.ClipboardCopyEnabled)
        {
            AddIssue(
                issues,
                "settings:clipboard-copy",
                null,
                HealthAuditSeverity.Info,
                HealthAuditCategory.ClipboardCopyEnabled,
                "Clipboard Settings",
                "Clipboard copy is enabled",
                "Copying secrets can expose them to the operating system clipboard. Clipboard clearing is best-effort, not a security boundary.",
                HealthAuditRecommendedAction.OpenSettings);
        }
    }

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

    private static string DescribeWeaknesses(string password)
    {
        var issues = new List<string>();
        if (password.Length < 12)
            issues.Add("shorter than 12 characters");
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
            : "This password is " + string.Join(", ", issues) + ". Use a unique generated password where possible.";
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

    private sealed record WebLoginHealthItem(
        string Id,
        string Title,
        string Username,
        string Password,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ApiSecretHealthItem(
        string ItemId,
        string Name,
        string SecretValue);
}
