using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Application.ProjectSecrets;

public enum ProjectSecretCompareStatus { Present, Missing, Empty, InvalidKey, Different, BrokenReference }

public sealed record ProjectSecretCompareCell(string ProfileId, string ProfileName, ProjectSecretCompareStatus Status);
public sealed record ProjectSecretCompareRow(string VariableKey, IReadOnlyList<ProjectSecretCompareCell> Cells);
public sealed record ProjectSecretCompareResult(IReadOnlyList<string> ProfileNames, IReadOnlyList<ProjectSecretCompareRow> Rows);

public static class ProjectSecretComparer
{
    public static ProjectSecretCompareResult Compare(
        ProjectSecretEnvironmentEntry environment,
        IReadOnlyList<string>? profileIds = null,
        Func<ProjectSecretVariableEntry, string?>? valueResolver = null)
    {
        var selectedIds = new HashSet<string>(profileIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var profiles = environment.Profiles
            .Where(profile => selectedIds.Count == 0 || selectedIds.Contains(profile.Id))
            .OrderBy(profile => profile.SortOrder)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var keys = profiles.SelectMany(profile => profile.Variables).Select(variable => variable.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToArray();
        var rows = new List<ProjectSecretCompareRow>();

        foreach (var key in keys)
        {
            var resolved = profiles.Select(profile => profile.Variables.FirstOrDefault(variable => string.Equals(variable.Key, key, StringComparison.OrdinalIgnoreCase)))
                .Where(variable => variable is not null)
                .Select(variable => Resolve(variable!, valueResolver))
                .Where(value => value is not null && value.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
            var differs = resolved.Length > 1;
            var cells = profiles.Select(profile =>
            {
                var variable = profile.Variables.FirstOrDefault(candidate => string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
                var value = variable is null ? null : Resolve(variable, valueResolver);
                var status = variable is null ? ProjectSecretCompareStatus.Missing
                    : !ProjectSecretValidator.IsValidVariableKey(variable.Key) ? ProjectSecretCompareStatus.InvalidKey
                    : variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey && value is null ? ProjectSecretCompareStatus.BrokenReference
                    : string.IsNullOrEmpty(value) ? ProjectSecretCompareStatus.Empty
                    : differs ? ProjectSecretCompareStatus.Different
                    : ProjectSecretCompareStatus.Present;
                return new ProjectSecretCompareCell(profile.Id, profile.Name, status);
            }).ToArray();
            rows.Add(new ProjectSecretCompareRow(key, cells));
        }

        return new ProjectSecretCompareResult(profiles.Select(profile => profile.Name).ToArray(), rows);
    }

    private static string? Resolve(ProjectSecretVariableEntry variable, Func<ProjectSecretVariableEntry, string?>? resolver)
        => variable.SourceKind == ProjectSecretVariableSourceKind.ReferencedApiKey ? resolver?.Invoke(variable) : variable.Value;
}
