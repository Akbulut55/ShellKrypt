namespace ShellKrypt.Core.Items;

public enum ProjectSecretEnvironmentKind
{
    Development = 1,
    Staging = 2,
    Production = 3,
    Local = 4,
    Test = 5,
    Preview = 6,
    QA = 7,
    Sandbox = 8,
    CI = 9
}

public enum ProjectSecretVariableSourceKind
{
    Manual = 1,
    LinkedApiKey = 2,
    ImportedApiKey = 3,
    ImportedEnvFile = 4
}

public enum ProjectSecretScanFindingKind
{
    UsedVariable = 1,
    UnusedVariable = 2,
    ReferencedButMissingVariable = 3,
    PossiblePlaintextLeak = 4,
    EnvFileWithValuesDetected = 5,
    SkippedLargeFile = 6,
    ScanLimitReached = 7,
    BrokenProjectRoot = 8
}

public sealed record ProjectSecretInput(
    string Name,
    string Description,
    string Notes,
    string? ProjectRootPath,
    IReadOnlyList<ProjectSecretEnvironmentInput> Environments,
    IReadOnlyList<ProjectSecretLinkedApiKeyInput> LinkedApiKeys,
    ProjectSecretScanResult? LastScanResult = null);

public sealed record ProjectSecretEntry(
    string Id,
    string Name,
    string Description,
    string Notes,
    string? ProjectRootPath,
    IReadOnlyList<ProjectSecretEnvironmentEntry> Environments,
    IReadOnlyList<ProjectSecretLinkedApiKeyEntry> LinkedApiKeys,
    string CreatedAtUtc,
    string UpdatedAtUtc,
    ProjectSecretScanResult? LastScanResult = null);

public sealed record ProjectSecretEnvironmentInput(
    string Id,
    string Name,
    ProjectSecretEnvironmentKind Kind,
    IReadOnlyList<ProjectSecretVariableInput> Variables,
    string Notes,
    int SortOrder,
    string ProfileName = "");

public sealed record ProjectSecretEnvironmentEntry(
    string Id,
    string Name,
    ProjectSecretEnvironmentKind Kind,
    IReadOnlyList<ProjectSecretVariableEntry> Variables,
    string Notes,
    int SortOrder,
    string ProfileName = "");

public sealed record ProjectSecretVariableInput(
    string Id,
    string Key,
    string Value,
    bool IsSecret,
    string Notes,
    int SortOrder,
    ProjectSecretVariableSourceKind SourceKind,
    string LinkedItemId,
    string LinkedFieldId,
    string LinkedFieldName,
    string? LastUpdatedAtUtc);

public sealed record ProjectSecretVariableEntry(
    string Id,
    string Key,
    string Value,
    bool IsSecret,
    string Notes,
    int SortOrder,
    ProjectSecretVariableSourceKind SourceKind,
    string LinkedItemId,
    string LinkedFieldId,
    string LinkedFieldName,
    string? LastUpdatedAtUtc);

public sealed record ProjectSecretLinkedApiKeyInput(
    string Id,
    string ApiKeyItemId,
    string ApiKeyFieldId,
    string VariableKey,
    string EnvironmentId,
    bool ImportCopy);

public sealed record ProjectSecretLinkedApiKeyEntry(
    string Id,
    string ApiKeyItemId,
    string ApiKeyFieldId,
    string VariableKey,
    string EnvironmentId,
    bool ImportCopy);

public sealed record ProjectSecretScanRequest(
    string ProjectId,
    string ProjectRootPath,
    IReadOnlyList<string> VariableKeys,
    IReadOnlyDictionary<string, string> SecretValues);

public sealed record ProjectSecretScanResult(
    string ProjectId,
    string ProjectRootPath,
    string StartedAtUtc,
    string CompletedAtUtc,
    int FilesScanned,
    int FilesSkipped,
    long BytesScanned,
    IReadOnlyList<ProjectSecretScanFinding> Findings);

public sealed record ProjectSecretScanFinding(
    ProjectSecretScanFindingKind Kind,
    HealthAuditSeverity Severity,
    string ProjectId,
    string? EnvironmentId,
    string? VariableId,
    string? VariableKey,
    string? RelativeFilePath,
    int? LineNumber,
    string Message);

public sealed record ProjectSecretAuditFinding(
    HealthAuditSeverity Severity,
    HealthAuditCategory Category,
    string ProjectId,
    string? EnvironmentId,
    string? VariableId,
    string? VariableKey,
    string Title,
    string Details);

public sealed record ProjectSecretPayload(
    string Name,
    string Description,
    string Notes,
    string? ProjectRootPath,
    IReadOnlyList<ProjectSecretEnvironmentPayload> Environments,
    IReadOnlyList<ProjectSecretProfilePayload> Profiles,
    IReadOnlyList<ProjectSecretVariablePayload> Variables,
    IReadOnlyList<ProjectSecretLinkedApiKeyPayload> LinkedApiKeys,
    ProjectSecretScanResult? LastScanResult = null);

public sealed record ProjectSecretEnvironmentPayload(
    string Id,
    string Name,
    string Notes,
    int SortOrder);

public sealed record ProjectSecretProfilePayload(
    string Id,
    string EnvironmentId,
    string Name,
    int SortOrder);

public sealed record ProjectSecretVariablePayload(
    string Id,
    string ProfileId,
    string Key,
    string Value,
    bool IsSecret,
    string Notes,
    int SortOrder,
    ProjectSecretVariableSourceKind SourceKind,
    string LinkedItemId,
    string LinkedFieldId,
    string LinkedFieldName,
    string? LastUpdatedAtUtc);

public sealed record ProjectSecretLinkedApiKeyPayload(
    string Id,
    string ApiKeyItemId,
    string ApiKeyFieldId,
    string VariableKey,
    string EnvironmentId,
    bool ImportCopy);

public interface IProjectSecretService
{
    Task<IReadOnlyList<ProjectSecretEntry>> ListAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default);
    Task<ProjectSecretEntry> AddAsync(string vaultPath, byte[] vaultKey, ProjectSecretInput input, CancellationToken ct = default);
    Task<ProjectSecretEntry> UpdateAsync(string vaultPath, byte[] vaultKey, string id, string createdAtUtc, ProjectSecretInput input, CancellationToken ct = default);
    Task DeleteAsync(string vaultPath, string id, CancellationToken ct = default);
}
