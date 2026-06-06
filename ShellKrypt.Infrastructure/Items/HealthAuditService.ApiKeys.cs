using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
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

    private sealed record ApiSecretHealthItem(
        string ItemId,
        string Name,
        string SecretValue);
}
