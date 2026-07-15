using ShellKrypt.Core.Items;

namespace ShellKrypt.Application.Items;

public sealed partial class VaultItemSummaryService
{
    private VaultItemSummary BuildProjectSecretSummary(VaultItemRow row, byte[] vaultKey, IReadOnlyList<string> labels)
    {
        var payload = _payloadReader.ReadProjectSecret(row, vaultKey);
        var title = FirstNonEmpty(payload.Name, "Untitled project");
        var environmentCount = payload.Environments.Count;
        var profiles = payload.Environments.SelectMany(environment => environment.Profiles).ToArray();
        var variables = profiles.SelectMany(profile => profile.Variables).ToArray();
        var variableCount = variables.Length;
        var warningCount = variables.Count(variable => string.IsNullOrWhiteSpace(variable.Value) && variable.SourceKind != ProjectSecretVariableSourceKind.ReferencedApiKey);
        var subtitle = $"{environmentCount} environment{Plural(environmentCount)}, {variableCount} variable{Plural(variableCount)}";
        var identifier = warningCount > 0 ? $"{warningCount} warning{Plural(warningCount)}" : FirstNonEmpty(payload.ProjectRootPath, "Project Secrets");
        var searchText = BuildSearchText(
            title,
            payload.Description,
            payload.Notes,
            payload.ProjectRootPath,
            string.Join(" ", payload.Environments.Select(environment => environment.Name)),
            string.Join(" ", profiles.Select(profile => profile.Name)),
            string.Join(" ", variables.Select(variable => variable.Key)),
            string.Join(" ", labels),
            row.Header.Favorite ? "favorite" : string.Empty);

        return new VaultItemSummary(
            row.Header.Id,
            row.Header.Type,
            title,
            subtitle,
            identifier,
            labels,
            searchText,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            row.Header.UpdatedAtUtc,
            title);
    }

    private static string Plural(int count)
        => count == 1 ? string.Empty : "s";
}
