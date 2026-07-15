using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed partial class ProjectSecretService
{
    private static ProjectSecretPayload ToPayload(ProjectSecretInput input)
    {
        var errors = ProjectSecretValidator.Validate(input);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        return new ProjectSecretPayload(
            NormalizeRequired(input.Name, "Project name is required."),
            input.Description.Trim(),
            input.Notes.Trim(),
            string.IsNullOrWhiteSpace(input.ProjectRootPath) ? null : input.ProjectRootPath.Trim(),
            input.Environments.Select(ToPayload).ToArray(),
            input.ScanResults.ToArray());
    }

    private static ProjectSecretEnvironmentPayload ToPayload(ProjectSecretEnvironmentInput environment)
        => new(
            NormalizeId(environment.Id),
            NormalizeRequired(environment.Name, "Environment name is required."),
            environment.Notes.Trim(),
            environment.SortOrder,
            environment.Profiles.Select(ToPayload).ToArray());

    private static ProjectSecretProfilePayload ToPayload(ProjectSecretProfileInput profile)
        => new(
            NormalizeId(profile.Id),
            NormalizeRequired(profile.Name, "Profile name is required."),
            profile.SortOrder,
            profile.Variables.Select(ToPayload).ToArray());

    private static ProjectSecretVariablePayload ToPayload(ProjectSecretVariableInput variable)
        => new(
            NormalizeId(variable.Id),
            NormalizeRequired(variable.Key, "Variable key is required."),
            variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey ? "" : variable.Value,
            variable.IsSecret,
            variable.Notes.Trim(),
            variable.SortOrder,
            variable.SourceKind,
            variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey ? variable.ReferencedItemId.Trim() : "",
            variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey ? variable.ReferencedFieldId.Trim() : "",
            variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey ? variable.ReferencedFieldName.Trim() : "",
            string.IsNullOrWhiteSpace(variable.LastUpdatedAtUtc) ? DateTimeOffset.UtcNow.ToString("O") : variable.LastUpdatedAtUtc.Trim());

    private static ProjectSecretEntry ToEntry(VaultItemHeader header, ProjectSecretPayload payload)
        => new(
            header.Id,
            payload.Name ?? "",
            payload.Description ?? "",
            payload.Notes ?? "",
            payload.ProjectRootPath,
            (payload.Environments ?? Array.Empty<ProjectSecretEnvironmentPayload>()).OrderBy(environment => environment.SortOrder).Select(ToEntry).ToArray(),
            (payload.ScanResults ?? Array.Empty<ProjectSecretScanResult>()).ToArray(),
            header.CreatedAtUtc,
            header.UpdatedAtUtc);

    private static ProjectSecretEnvironmentEntry ToEntry(ProjectSecretEnvironmentPayload environment)
        => new(
            environment.Id ?? "",
            environment.Name ?? "",
            environment.Notes ?? "",
            environment.SortOrder,
            (environment.Profiles ?? Array.Empty<ProjectSecretProfilePayload>()).OrderBy(profile => profile.SortOrder).Select(ToEntry).ToArray());

    private static ProjectSecretProfileEntry ToEntry(ProjectSecretProfilePayload profile)
        => new(
            profile.Id ?? "",
            profile.Name ?? "",
            profile.SortOrder,
            (profile.Variables ?? Array.Empty<ProjectSecretVariablePayload>()).OrderBy(variable => variable.SortOrder).Select(ToEntry).ToArray());

    private static ProjectSecretVariableEntry ToEntry(ProjectSecretVariablePayload variable)
        => new(
            variable.Id ?? "",
            variable.Key ?? "",
            variable.Value ?? "",
            variable.IsSecret,
            variable.Notes ?? "",
            variable.SortOrder,
            variable.SourceKind,
            variable.ReferencedItemId ?? "",
            variable.ReferencedFieldId ?? "",
            variable.ReferencedFieldName ?? "",
            variable.LastUpdatedAtUtc);

    private static string NormalizeId(string? value)
        => string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException(errorMessage);
        return trimmed;
    }
}
