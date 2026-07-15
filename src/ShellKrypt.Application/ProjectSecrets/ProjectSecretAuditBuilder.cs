using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Application.ProjectSecrets;

public static class ProjectSecretAuditBuilder
{
    public static IReadOnlyList<ProjectSecretAuditFinding> BuildFindings(ProjectSecretEntry project)
        => ProjectSecretValidator.BuildValidationFindings(project)
            .Concat(BuildDriftFindings(project))
            .Concat(BuildScanFindings(project))
            .ToArray();

    private static IEnumerable<ProjectSecretAuditFinding> BuildDriftFindings(ProjectSecretEntry project)
    {
        foreach (var environment in project.Environments)
        {
            var profiles = environment.Profiles.OrderBy(profile => profile.SortOrder).ToArray();
            if (profiles.Length < 2)
                continue;
            var keys = profiles.SelectMany(profile => profile.Variables).Select(variable => variable.Key).Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            foreach (var profile in profiles.Where(profile => profile.Variables.All(variable => !string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase))))
                yield return new ProjectSecretAuditFinding(HealthAuditSeverity.Medium, HealthAuditCategory.ProjectSecretMissingVariable, project.Id, environment.Id, profile.Id, null, key, "Project Secret missing in profile", $"{key} is missing in {environment.Name} / {profile.Name}.");
        }
    }

    private static IEnumerable<ProjectSecretAuditFinding> BuildScanFindings(ProjectSecretEntry project)
    {
        foreach (var result in project.ScanResults)
        foreach (var finding in result.Findings)
        {
            var category = finding.Kind switch
            {
                ProjectSecretScanFindingKind.UnusedVariable => HealthAuditCategory.ProjectSecretUnusedVariable,
                ProjectSecretScanFindingKind.ReferencedButMissingVariable => HealthAuditCategory.ProjectSecretMissingStoredVariableReferencedByProject,
                ProjectSecretScanFindingKind.PossiblePlaintextLeak => HealthAuditCategory.ProjectSecretPossiblePlaintextLeak,
                ProjectSecretScanFindingKind.EnvFileWithValuesDetected => HealthAuditCategory.ProjectSecretPlaintextExportRisk,
                _ => (HealthAuditCategory?)null
            };
            if (category is not null)
                yield return new ProjectSecretAuditFinding(finding.Severity, category.Value, project.Id, finding.EnvironmentId, finding.ProfileId, finding.VariableId, finding.VariableKey, "Project Secret scan finding", finding.Message);
        }
    }
}
