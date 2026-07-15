using ShellKrypt.Core.Items;

namespace ShellKrypt.Core.ProjectSecrets;

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

public sealed record ProjectSecretScanRequest(
    string ProjectId,
    string EnvironmentId,
    string ProfileId,
    string ProjectRootPath,
    IReadOnlyList<string> VariableKeys,
    IReadOnlyDictionary<string, string> SecretValues);

public sealed record ProjectSecretScanResult(
    string ProjectId,
    string EnvironmentId,
    string ProfileId,
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
    string EnvironmentId,
    string ProfileId,
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
    string? ProfileId,
    string? VariableId,
    string? VariableKey,
    string Title,
    string Details);
