using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.ProjectSecrets;

public static class ProjectSecretAuditBuilder
{
    public static IReadOnlyList<ProjectSecretAuditFinding> BuildFindings(ProjectSecretEntry project)
    {
        var findings = new List<ProjectSecretAuditFinding>();
        findings.AddRange(ProjectSecretValidator.BuildValidationFindings(project));
        findings.AddRange(BuildDriftFindings(project));
        findings.AddRange(BuildScanFindings(project));
        return findings;
    }

    private static IEnumerable<ProjectSecretAuditFinding> BuildDriftFindings(ProjectSecretEntry project)
    {
        var environments = project.Environments.OrderBy(environment => environment.SortOrder).ToArray();
        if (environments.Length < 2)
            yield break;

        var keys = environments
            .SelectMany(environment => environment.Variables.Select(variable => variable.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var key in keys)
        {
            var missing = environments
                .Where(environment => environment.Variables.All(variable => !string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            foreach (var environment in missing)
            {
                yield return new ProjectSecretAuditFinding(
                    HealthAuditSeverity.Medium,
                    HealthAuditCategory.ProjectSecretMissingVariable,
                    project.Id,
                    environment.Id,
                    null,
                    key,
                    "Project Secret missing in environment",
                    $"{key} is missing in {environment.Name}.");
            }
        }
    }

    private static IEnumerable<ProjectSecretAuditFinding> BuildScanFindings(ProjectSecretEntry project)
    {
        if (project.LastScanResult is null)
            yield break;

        foreach (var finding in project.LastScanResult.Findings)
        {
            var category = finding.Kind switch
            {
                ProjectSecretScanFindingKind.UnusedVariable => HealthAuditCategory.ProjectSecretUnusedVariable,
                ProjectSecretScanFindingKind.ReferencedButMissingVariable => HealthAuditCategory.ProjectSecretMissingStoredVariableReferencedByProject,
                ProjectSecretScanFindingKind.PossiblePlaintextLeak => HealthAuditCategory.ProjectSecretPossiblePlaintextLeak,
                ProjectSecretScanFindingKind.EnvFileWithValuesDetected => HealthAuditCategory.ProjectSecretPlaintextExportRisk,
                _ => (HealthAuditCategory?)null
            };

            if (category is null)
                continue;

            yield return new ProjectSecretAuditFinding(
                finding.Severity,
                category.Value,
                project.Id,
                finding.EnvironmentId,
                finding.VariableId,
                finding.VariableKey,
                "Project Secret scan finding",
                finding.Message);
        }
    }
}
