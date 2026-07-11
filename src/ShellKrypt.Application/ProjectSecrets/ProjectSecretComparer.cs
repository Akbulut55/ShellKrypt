using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.ProjectSecrets;

public enum ProjectSecretCompareStatus
{
    Present,
    Missing,
    Empty,
    InvalidKey,
    Different,
    BrokenLink
}

public sealed record ProjectSecretCompareCell(
    string EnvironmentId,
    string EnvironmentName,
    ProjectSecretCompareStatus Status);

public sealed record ProjectSecretCompareRow(
    string VariableKey,
    IReadOnlyList<ProjectSecretCompareCell> Cells);

public sealed record ProjectSecretCompareResult(
    IReadOnlyList<string> EnvironmentNames,
    IReadOnlyList<ProjectSecretCompareRow> Rows);

public static class ProjectSecretComparer
{
    public static ProjectSecretCompareResult Compare(ProjectSecretEntry project, IReadOnlyList<string>? environmentIds = null)
    {
        var selectedIds = new HashSet<string>(environmentIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var environments = project.Environments
            .Where(environment => selectedIds.Count == 0 || selectedIds.Contains(environment.Id))
            .OrderBy(environment => environment.SortOrder)
            .ThenBy(environment => environment.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var keys = environments
            .SelectMany(environment => environment.Variables.Select(variable => variable.Key))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = new List<ProjectSecretCompareRow>();
        foreach (var key in keys)
        {
            var nonEmptyValues = environments
                .Select(environment => environment.Variables.FirstOrDefault(variable => string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase)))
                .Where(variable => variable is not null && !string.IsNullOrEmpty(variable.Value) && variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey)
                .Select(variable => variable!.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var differs = nonEmptyValues.Length > 1;

            var cells = new List<ProjectSecretCompareCell>();
            foreach (var environment in environments)
            {
                var variable = environment.Variables.FirstOrDefault(variable => string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase));
                var status = variable is null
                    ? ProjectSecretCompareStatus.Missing
                    : !ProjectSecretValidator.IsValidVariableKey(variable.Key)
                        ? ProjectSecretCompareStatus.InvalidKey
                        : variable.SourceKind == ProjectSecretVariableSourceKind.LinkedApiKey &&
                          (string.IsNullOrWhiteSpace(variable.LinkedItemId) || string.IsNullOrWhiteSpace(variable.LinkedFieldId))
                            ? ProjectSecretCompareStatus.BrokenLink
                            : string.IsNullOrEmpty(variable.Value) && variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey
                                ? ProjectSecretCompareStatus.Empty
                                : differs
                                    ? ProjectSecretCompareStatus.Different
                                    : ProjectSecretCompareStatus.Present;

                cells.Add(new ProjectSecretCompareCell(environment.Id, environment.Name, status));
            }

            rows.Add(new ProjectSecretCompareRow(key, cells));
        }

        return new ProjectSecretCompareResult(environments.Select(environment => environment.Name).ToArray(), rows);
    }
}
