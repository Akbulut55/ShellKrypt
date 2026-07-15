using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Application.ProjectSecrets;

public interface IProjectSecretEnvParser
{
    ProjectSecretEnvParseResult Parse(string content);
    ProjectSecretEnvImportPreview BuildPreview(ProjectSecretEnvParseResult parseResult, IReadOnlyCollection<string> existingKeys);
}

public interface IProjectSecretEnvWriter
{
    string WriteEnvironment(IEnumerable<ProjectSecretVariableEntry> variables, Func<ProjectSecretVariableEntry, string> valueResolver);
    string WriteTemplate(IEnumerable<ProjectSecretVariableEntry> variables);
}

public interface IProjectSecretScanner
{
    Task<ProjectSecretScanResult> ScanAsync(ProjectSecretScanRequest request, CancellationToken ct = default);
}
