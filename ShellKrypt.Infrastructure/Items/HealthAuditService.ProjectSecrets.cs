using System.Text.Json;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
    private static void AddProjectSecretFindings(
        VaultItemRow row,
        byte[] vaultKey,
        List<HealthAuditIssue> issues,
        HashSet<string> apiKeyFieldIds)
    {
        var payload = ProjectSecretPayloadCompatibility.Deserialize(
            VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
            JsonOpts);

        var entry = ToProjectSecretEntry(row.Header, payload);
        foreach (var finding in ProjectSecretAuditBuilder.BuildFindings(entry))
        {
            AddIssue(
                issues,
                row.Header.Id,
                ItemType.ProjectSecret,
                finding.Severity,
                finding.Category,
                entry.Name,
                finding.Title,
                finding.Details,
                HealthAuditRecommendedAction.OpenProjectSecret);
        }

        foreach (var variable in entry.Environments.SelectMany(environment => environment.Variables))
        {
            if (variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey)
                continue;

            var key = BuildApiFieldLookupKey(variable.LinkedItemId, variable.LinkedFieldId);
            if (apiKeyFieldIds.Contains(key))
                continue;

            AddIssue(
                issues,
                row.Header.Id,
                ItemType.ProjectSecret,
                HealthAuditSeverity.High,
                HealthAuditCategory.ProjectSecretBrokenApiKeyLink,
                entry.Name,
                "Broken API Key link",
                $"{variable.Key} links to an API Key field that no longer exists.",
                HealthAuditRecommendedAction.OpenProjectSecret);
        }
    }

    private static HashSet<string> BuildApiKeyFieldIdSet(IReadOnlyList<VaultItemRow> rows, byte[] vaultKey)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.Where(row => row.Header.Type == ItemType.ApiKey))
        {
            var payload = JsonSerializer.Deserialize<ApiKeyPayload>(
                VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload),
                JsonOpts);
            if (payload is null)
                continue;

            foreach (var field in payload.Fields)
                set.Add(BuildApiFieldLookupKey(row.Header.Id, field.Id));
        }

        return set;
    }

    private static ProjectSecretEntry ToProjectSecretEntry(VaultItemHeader header, ProjectSecretPayload payload)
        => new(
            header.Id,
            payload.Name,
            payload.Description,
            payload.Notes,
            payload.ProjectRootPath,
            payload.Environments
                .OrderBy(environment => environment.SortOrder)
                .SelectMany(environment => payload.Profiles
                    .Where(profile => string.Equals(profile.EnvironmentId, environment.Id, StringComparison.Ordinal))
                    .OrderBy(profile => profile.SortOrder)
                    .Select(profile => new ProjectSecretEnvironmentEntry(
                        profile.Id,
                        environment.Name,
                        ProfileKind(profile.Name),
                        payload.Variables
                            .Where(variable => string.Equals(variable.ProfileId, profile.Id, StringComparison.Ordinal))
                            .OrderBy(variable => variable.SortOrder)
                            .Select(variable => new ProjectSecretVariableEntry(
                                variable.Id,
                                variable.Key,
                                variable.Value,
                                variable.IsSecret,
                                variable.Notes,
                                variable.SortOrder,
                                variable.SourceKind,
                                variable.LinkedItemId,
                                variable.LinkedFieldId,
                                variable.LinkedFieldName,
                                variable.LastUpdatedAtUtc))
                            .ToArray(),
                        environment.Notes,
                        profile.SortOrder,
                        profile.Name)))
                .ToArray(),
            payload.LinkedApiKeys.Select(link => new ProjectSecretLinkedApiKeyEntry(
                link.Id,
                link.ApiKeyItemId,
                link.ApiKeyFieldId,
                link.VariableKey,
                link.EnvironmentId,
                link.ImportCopy)).ToArray(),
            header.CreatedAtUtc,
            header.UpdatedAtUtc,
            payload.LastScanResult);

    private static ProjectSecretEnvironmentKind ProfileKind(string profileName)
        => Enum.TryParse<ProjectSecretEnvironmentKind>(profileName, true, out var kind)
            ? kind
            : ProjectSecretEnvironmentKind.Development;

    private static string BuildApiFieldLookupKey(string itemId, string fieldId)
        => $"{itemId.Trim()}|{fieldId.Trim()}";
}
