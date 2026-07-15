namespace ShellKrypt.Core.ProjectSecrets;

public enum ProjectSecretVariableSourceKind
{
    Manual = 1,
    ReferencedApiKey = 2,
    ImportedApiKey = 3,
    ImportedEnvFile = 4
}

public sealed record ProjectSecretInput(
    string Name,
    string Description,
    string Notes,
    string? ProjectRootPath,
    IReadOnlyList<ProjectSecretEnvironmentInput> Environments,
    IReadOnlyList<ProjectSecretScanResult> ScanResults);

public sealed record ProjectSecretEntry(
    string Id,
    string Name,
    string Description,
    string Notes,
    string? ProjectRootPath,
    IReadOnlyList<ProjectSecretEnvironmentEntry> Environments,
    IReadOnlyList<ProjectSecretScanResult> ScanResults,
    string CreatedAtUtc,
    string UpdatedAtUtc);

public sealed record ProjectSecretEnvironmentInput(
    string Id,
    string Name,
    string Notes,
    int SortOrder,
    IReadOnlyList<ProjectSecretProfileInput> Profiles);

public sealed record ProjectSecretEnvironmentEntry(
    string Id,
    string Name,
    string Notes,
    int SortOrder,
    IReadOnlyList<ProjectSecretProfileEntry> Profiles);

public sealed record ProjectSecretProfileInput(
    string Id,
    string Name,
    int SortOrder,
    IReadOnlyList<ProjectSecretVariableInput> Variables);

public sealed record ProjectSecretProfileEntry(
    string Id,
    string Name,
    int SortOrder,
    IReadOnlyList<ProjectSecretVariableEntry> Variables);

public sealed record ProjectSecretVariableInput(
    string Id,
    string Key,
    string Value,
    bool IsSecret,
    string Notes,
    int SortOrder,
    ProjectSecretVariableSourceKind SourceKind,
    string ReferencedItemId,
    string ReferencedFieldId,
    string ReferencedFieldName,
    string? LastUpdatedAtUtc);

public sealed record ProjectSecretVariableEntry(
    string Id,
    string Key,
    string Value,
    bool IsSecret,
    string Notes,
    int SortOrder,
    ProjectSecretVariableSourceKind SourceKind,
    string ReferencedItemId,
    string ReferencedFieldId,
    string ReferencedFieldName,
    string? LastUpdatedAtUtc);

public sealed record ProjectSecretPayload(
    string Name,
    string Description,
    string Notes,
    string? ProjectRootPath,
    IReadOnlyList<ProjectSecretEnvironmentPayload> Environments,
    IReadOnlyList<ProjectSecretScanResult> ScanResults);

public sealed record ProjectSecretEnvironmentPayload(
    string Id,
    string Name,
    string Notes,
    int SortOrder,
    IReadOnlyList<ProjectSecretProfilePayload> Profiles);

public sealed record ProjectSecretProfilePayload(
    string Id,
    string Name,
    int SortOrder,
    IReadOnlyList<ProjectSecretVariablePayload> Variables);

public sealed record ProjectSecretVariablePayload(
    string Id,
    string Key,
    string Value,
    bool IsSecret,
    string Notes,
    int SortOrder,
    ProjectSecretVariableSourceKind SourceKind,
    string ReferencedItemId,
    string ReferencedFieldId,
    string ReferencedFieldName,
    string? LastUpdatedAtUtc);
