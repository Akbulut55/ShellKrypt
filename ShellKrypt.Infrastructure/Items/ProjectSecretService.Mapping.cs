using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class ProjectSecretService
{
    private static ProjectSecretPayload ToPayload(ProjectSecretInput input)
    {
        var errors = ProjectSecretValidator.Validate(input);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        return new ProjectSecretPayload(
            Name: NormalizeRequired(input.Name, "Project name is required."),
            Description: input.Description.Trim(),
            Notes: input.Notes.Trim(),
            ProjectRootPath: string.IsNullOrWhiteSpace(input.ProjectRootPath) ? null : input.ProjectRootPath.Trim(),
            Environments: NormalizeEnvironments(input.Environments).ToArray(),
            LinkedApiKeys: NormalizeLinkedApiKeys(input.LinkedApiKeys).ToArray(),
            LastScanResult: input.LastScanResult);
    }

    private static IEnumerable<ProjectSecretEnvironmentPayload> NormalizeEnvironments(IEnumerable<ProjectSecretEnvironmentInput>? environments)
    {
        var order = 0;
        foreach (var environment in environments ?? Array.Empty<ProjectSecretEnvironmentInput>())
        {
            yield return new ProjectSecretEnvironmentPayload(
                Id: string.IsNullOrWhiteSpace(environment.Id) ? Guid.NewGuid().ToString("N") : environment.Id.Trim(),
                Name: NormalizeRequired(environment.Name, "Environment name is required."),
                Kind: environment.Kind,
                Variables: NormalizeVariables(environment.Variables).ToArray(),
                Notes: environment.Notes.Trim(),
                SortOrder: environment.SortOrder <= 0 ? order : environment.SortOrder);
            order++;
        }
    }

    private static IEnumerable<ProjectSecretVariablePayload> NormalizeVariables(IEnumerable<ProjectSecretVariableInput>? variables)
    {
        var order = 0;
        foreach (var variable in variables ?? Array.Empty<ProjectSecretVariableInput>())
        {
            if (string.IsNullOrWhiteSpace(variable.Key) && string.IsNullOrWhiteSpace(variable.Value))
                continue;

            yield return new ProjectSecretVariablePayload(
                Id: string.IsNullOrWhiteSpace(variable.Id) ? Guid.NewGuid().ToString("N") : variable.Id.Trim(),
                Key: NormalizeRequired(variable.Key, "Variable key is required."),
                Value: variable.Value,
                IsSecret: variable.IsSecret,
                Notes: variable.Notes.Trim(),
                SortOrder: variable.SortOrder <= 0 ? order : variable.SortOrder,
                SourceKind: variable.SourceKind,
                LinkedItemId: variable.LinkedItemId.Trim(),
                LinkedFieldId: variable.LinkedFieldId.Trim(),
                LinkedFieldName: variable.LinkedFieldName.Trim(),
                LastUpdatedAtUtc: string.IsNullOrWhiteSpace(variable.LastUpdatedAtUtc)
                    ? DateTimeOffset.UtcNow.ToString("O")
                    : variable.LastUpdatedAtUtc.Trim());
            order++;
        }
    }

    private static IEnumerable<ProjectSecretLinkedApiKeyPayload> NormalizeLinkedApiKeys(IEnumerable<ProjectSecretLinkedApiKeyInput>? linkedApiKeys)
    {
        foreach (var link in linkedApiKeys ?? Array.Empty<ProjectSecretLinkedApiKeyInput>())
        {
            if (string.IsNullOrWhiteSpace(link.ApiKeyItemId) || string.IsNullOrWhiteSpace(link.ApiKeyFieldId))
                continue;

            yield return new ProjectSecretLinkedApiKeyPayload(
                string.IsNullOrWhiteSpace(link.Id) ? Guid.NewGuid().ToString("N") : link.Id.Trim(),
                link.ApiKeyItemId.Trim(),
                link.ApiKeyFieldId.Trim(),
                link.VariableKey.Trim(),
                link.EnvironmentId.Trim(),
                link.ImportCopy);
        }
    }

    private static ProjectSecretEntry ToEntry(VaultItemHeader header, ProjectSecretPayload payload)
        => new(
            Id: header.Id,
            Name: payload.Name,
            Description: payload.Description,
            Notes: payload.Notes,
            ProjectRootPath: payload.ProjectRootPath,
            Environments: payload.Environments
                .OrderBy(environment => environment.SortOrder)
                .Select(environment => new ProjectSecretEnvironmentEntry(
                    environment.Id,
                    environment.Name,
                    environment.Kind,
                    environment.Variables
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
                    environment.SortOrder))
                .ToArray(),
            LinkedApiKeys: payload.LinkedApiKeys
                .Select(link => new ProjectSecretLinkedApiKeyEntry(link.Id, link.ApiKeyItemId, link.ApiKeyFieldId, link.VariableKey, link.EnvironmentId, link.ImportCopy))
                .ToArray(),
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc,
            LastScanResult: payload.LastScanResult);

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException(errorMessage);

        return trimmed;
    }
}
