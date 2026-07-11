namespace ShellKrypt.Application.ProjectSecrets;

public enum ProjectSecretEnvRowStatus
{
    New = 1,
    Conflict = 2,
    Duplicate = 3,
    Invalid = 4
}

public sealed record ProjectSecretEnvVariable(
    string Key,
    string Value,
    int LineNumber);

public sealed record ProjectSecretEnvParseIssue(
    int LineNumber,
    string Message);

public sealed record ProjectSecretEnvParseResult(
    IReadOnlyList<ProjectSecretEnvVariable> Variables,
    IReadOnlyList<ProjectSecretEnvParseIssue> Issues);

public sealed record ProjectSecretEnvImportPreviewRow(
    int LineNumber,
    string Key,
    string Value,
    ProjectSecretEnvRowStatus Status,
    string Message);

public sealed record ProjectSecretEnvImportPreview(
    int TotalRows,
    int NewRows,
    int ConflictRows,
    int DuplicateRows,
    int InvalidRows,
    IReadOnlyList<ProjectSecretEnvImportPreviewRow> Rows);

public enum ProjectSecretEnvImportConflictStrategy
{
    ReplaceExisting = 1,
    SkipExisting = 2
}
