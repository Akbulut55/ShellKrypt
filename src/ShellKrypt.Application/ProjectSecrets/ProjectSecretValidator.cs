using System.Text.RegularExpressions;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Application.ProjectSecrets;

public static class ProjectSecretValidator
{
    public const string VariableKeyPattern = "^[A-Za-z_][A-Za-z0-9_]*$";
    private static readonly Regex VariableKeyRegex = new(VariableKeyPattern, RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(ProjectSecretInput input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name))
            errors.Add("Project name is required.");

        AddDuplicateErrors(input.Environments, environment => environment.Name, "environment", errors);
        foreach (var environment in input.Environments)
        {
            if (string.IsNullOrWhiteSpace(environment.Name))
                errors.Add("Environment name is required.");

            AddDuplicateErrors(environment.Profiles, profile => profile.Name, $"profile in {environment.Name.Trim()}", errors);
            foreach (var profile in environment.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name))
                    errors.Add($"Profile name is required in {environment.Name.Trim()}.");

                AddDuplicateErrors(profile.Variables, variable => variable.Key, $"variable in {environment.Name.Trim()} / {profile.Name.Trim()}", errors);
                foreach (var variable in profile.Variables)
                {
                    if (string.IsNullOrWhiteSpace(variable.Key))
                        errors.Add($"Variable key is required in {environment.Name.Trim()} / {profile.Name.Trim()}.");
                    if (variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey &&
                        (string.IsNullOrWhiteSpace(variable.ReferencedItemId) || string.IsNullOrWhiteSpace(variable.ReferencedFieldId)))
                        errors.Add($"Referenced API Key is incomplete for {variable.Key.Trim()}.");
                }
            }
        }

        return errors;
    }

    public static bool IsValidVariableKey(string key)
        => VariableKeyRegex.IsMatch((key ?? "").Trim());

    public static IReadOnlyList<ProjectSecretAuditFinding> BuildValidationFindings(ProjectSecretEntry project)
    {
        var findings = new List<ProjectSecretAuditFinding>();
        foreach (var environment in project.Environments)
        foreach (var profile in environment.Profiles)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in profile.Variables)
            {
                if (!seen.Add(variable.Key))
                    findings.Add(CreateFinding(HealthAuditSeverity.Medium, HealthAuditCategory.ProjectSecretDuplicateKey, project, environment, profile, variable, "Duplicate Project Secret variable", $"{variable.Key} appears more than once in {environment.Name} / {profile.Name}."));
                if (!IsValidVariableKey(variable.Key))
                    findings.Add(CreateFinding(HealthAuditSeverity.Low, HealthAuditCategory.ProjectSecretInvalidKey, project, environment, profile, variable, "Invalid Project Secret variable name", $"{variable.Key} does not match the recommended environment variable format."));
                if (string.IsNullOrEmpty(variable.Value) && variable.SourceKind != ProjectSecretVariableSourceKind.ReferencedApiKey)
                    findings.Add(CreateFinding(HealthAuditSeverity.Low, HealthAuditCategory.ProjectSecretEmptyValue, project, environment, profile, variable, "Empty Project Secret value", $"{variable.Key} is stored with an empty value in {environment.Name} / {profile.Name}."));
                if (variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey &&
                    (string.IsNullOrWhiteSpace(variable.ReferencedItemId) || string.IsNullOrWhiteSpace(variable.ReferencedFieldId)))
                    findings.Add(CreateFinding(HealthAuditSeverity.Medium, HealthAuditCategory.ProjectSecretBrokenApiKeyLink, project, environment, profile, variable, "Broken API Key reference", $"{variable.Key} has an incomplete API Key reference."));
            }
        }
        return findings;
    }

    private static void AddDuplicateErrors<T>(IEnumerable<T> values, Func<T, string> key, string label, ICollection<string> errors)
    {
        foreach (var duplicate in values.Select(key).Select(value => value.Trim()).Where(value => value.Length > 0).GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            errors.Add($"Duplicate {label}: {duplicate.Key}.");
    }

    private static ProjectSecretAuditFinding CreateFinding(HealthAuditSeverity severity, HealthAuditCategory category, ProjectSecretEntry project, ProjectSecretEnvironmentEntry environment, ProjectSecretProfileEntry profile, ProjectSecretVariableEntry variable, string title, string details)
        => new(severity, category, project.Id, environment.Id, profile.Id, variable.Id, variable.Key, title, details);
}
