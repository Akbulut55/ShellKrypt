using System.Text.RegularExpressions;
using ShellKrypt.Core.Items;

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

        var environments = input.Environments ?? Array.Empty<ProjectSecretEnvironmentInput>();
        if (environments.Count == 0)
            errors.Add("At least one environment is required.");

        foreach (var duplicate in environments
                     .Select(environment => environment.Name.Trim())
                     .Where(name => name.Length > 0)
                     .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Duplicate environment name: {duplicate.Key}.");
        }

        foreach (var environment in environments)
        {
            if (string.IsNullOrWhiteSpace(environment.Name))
                errors.Add("Environment name is required.");

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in environment.Variables ?? Array.Empty<ProjectSecretVariableInput>())
            {
                var key = variable.Key.Trim();
                if (key.Length == 0)
                {
                    errors.Add($"Variable key is required in {environment.Name.Trim()}.");
                    continue;
                }

                if (!keys.Add(key))
                    errors.Add($"Duplicate variable key in {environment.Name.Trim()}: {key}.");
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
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in environment.Variables)
            {
                if (!seen.Add(variable.Key))
                {
                    findings.Add(CreateFinding(
                        HealthAuditSeverity.Medium,
                        HealthAuditCategory.ProjectSecretDuplicateKey,
                        project,
                        environment,
                        variable,
                        "Duplicate Project Secret variable",
                        $"{variable.Key} appears more than once in {environment.Name}."));
                }

                if (!IsValidVariableKey(variable.Key))
                {
                    findings.Add(CreateFinding(
                        HealthAuditSeverity.Low,
                        HealthAuditCategory.ProjectSecretInvalidKey,
                        project,
                        environment,
                        variable,
                        "Invalid Project Secret variable name",
                        $"{variable.Key} does not match the recommended environment variable format."));
                }

                if (string.IsNullOrEmpty(variable.Value) && variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey)
                {
                    findings.Add(CreateFinding(
                        HealthAuditSeverity.Low,
                        HealthAuditCategory.ProjectSecretEmptyValue,
                        project,
                        environment,
                        variable,
                        "Empty Project Secret value",
                        $"{variable.Key} is stored with an empty value in {environment.Name}."));
                }
            }
        }

        return findings;
    }

    private static ProjectSecretAuditFinding CreateFinding(
        HealthAuditSeverity severity,
        HealthAuditCategory category,
        ProjectSecretEntry project,
        ProjectSecretEnvironmentEntry environment,
        ProjectSecretVariableEntry variable,
        string title,
        string details)
        => new(severity, category, project.Id, environment.Id, variable.Id, variable.Key, title, details);
}
