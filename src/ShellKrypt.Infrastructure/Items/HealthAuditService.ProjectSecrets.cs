using System.Text.Json;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class HealthAuditService
{
    private static void AddProjectSecretFindings(VaultItemRow row, byte[] vaultKey, List<HealthAuditIssue> issues, HashSet<string> apiKeyFieldIds)
    {
        var payload = JsonSerializer.Deserialize<ProjectSecretPayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload), JsonOpts);
        if (payload is null)
            return;

        var entry = ToProjectSecretEntry(row.Header, payload);
        foreach (var finding in ProjectSecretAuditBuilder.BuildFindings(entry))
            AddIssue(issues, row.Header.Id, ItemType.ProjectSecret, finding.Severity, finding.Category, entry.Name, finding.Title, finding.Details, HealthAuditRecommendedAction.OpenProjectSecret);

        foreach (var variable in entry.Environments.SelectMany(environment => environment.Profiles).SelectMany(profile => profile.Variables))
        {
            if (variable.SourceKind != ProjectSecretVariableSourceKind.ReferencedApiKey)
                continue;
            if (apiKeyFieldIds.Contains(BuildApiFieldLookupKey(variable.ReferencedItemId, variable.ReferencedFieldId)))
                continue;
            AddIssue(issues, row.Header.Id, ItemType.ProjectSecret, HealthAuditSeverity.High, HealthAuditCategory.ProjectSecretBrokenApiKeyLink, entry.Name, "Broken API Key reference", $"{variable.Key} references an API Key field that no longer exists.", HealthAuditRecommendedAction.OpenProjectSecret);
        }
    }

    private static HashSet<string> BuildApiKeyFieldIdSet(IReadOnlyList<VaultItemRow> rows, byte[] vaultKey)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows.Where(row => row.Header.Type == ItemType.ApiKey))
        {
            var payload = JsonSerializer.Deserialize<ApiKeyPayload>(VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload), JsonOpts);
            if (payload is null)
                continue;
            foreach (var field in payload.Fields)
                set.Add(BuildApiFieldLookupKey(row.Header.Id, field.Id));
        }
        return set;
    }

    private static ProjectSecretEntry ToProjectSecretEntry(VaultItemHeader header, ProjectSecretPayload payload)
        => new(header.Id, payload.Name, payload.Description, payload.Notes, payload.ProjectRootPath,
            payload.Environments.OrderBy(environment => environment.SortOrder).Select(environment =>
                new ProjectSecretEnvironmentEntry(environment.Id, environment.Name, environment.Notes, environment.SortOrder,
                    environment.Profiles.OrderBy(profile => profile.SortOrder).Select(profile =>
                        new ProjectSecretProfileEntry(profile.Id, profile.Name, profile.SortOrder,
                            profile.Variables.OrderBy(variable => variable.SortOrder).Select(variable =>
                                new ProjectSecretVariableEntry(variable.Id, variable.Key, variable.Value, variable.IsSecret, variable.Notes, variable.SortOrder, variable.SourceKind, variable.ReferencedItemId, variable.ReferencedFieldId, variable.ReferencedFieldName, variable.LastUpdatedAtUtc)).ToArray())).ToArray())).ToArray(),
            payload.ScanResults.ToArray(), header.CreatedAtUtc, header.UpdatedAtUtc);

    private static string BuildApiFieldLookupKey(string itemId, string fieldId)
        => $"{itemId.Trim()}|{fieldId.Trim()}";
}
