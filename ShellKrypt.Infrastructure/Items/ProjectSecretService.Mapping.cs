using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using System.Security.Cryptography;
using System.Text;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class ProjectSecretService
{
    private static ProjectSecretPayload ToPayload(ProjectSecretInput input)
    {
        var errors = ProjectSecretValidator.Validate(input);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        return new ProjectSecretPayload(
            Name: NormalizeRequired(input.Name, "Project name is required."),
            Description: input.Description.Trim(),
            Notes: input.Notes.Trim(),
            ProjectRootPath: string.IsNullOrWhiteSpace(input.ProjectRootPath) ? null : input.ProjectRootPath.Trim(),
            Environments: NormalizeEnvironmentGroups(input.Environments).ToArray(),
            Profiles: NormalizeProfiles(input.Environments).ToArray(),
            Variables: NormalizeVariables(input.Environments).ToArray(),
            LinkedApiKeys: NormalizeLinkedApiKeys(input.LinkedApiKeys).ToArray(),
            LastScanResult: input.LastScanResult);
    }

    private static IEnumerable<ProjectSecretEnvironmentPayload> NormalizeEnvironmentGroups(IEnumerable<ProjectSecretEnvironmentInput>? environments)
    {
        var order = 0;
        foreach (var group in (environments ?? Array.Empty<ProjectSecretEnvironmentInput>())
                     .Where(environment => !string.IsNullOrWhiteSpace(environment.Name))
                     .GroupBy(environment => NormalizeRequired(environment.Name, "Environment name is required."), StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            yield return new ProjectSecretEnvironmentPayload(
                Id: EnvironmentGroupId(group.Key),
                Name: group.Key,
                Notes: first.Notes.Trim(),
                SortOrder: first.SortOrder <= 0 ? order : first.SortOrder);
            order++;
        }
    }

    private static IEnumerable<ProjectSecretProfilePayload> NormalizeProfiles(IEnumerable<ProjectSecretEnvironmentInput>? environments)
    {
        var orderByEnvironment = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var environment in environments ?? Array.Empty<ProjectSecretEnvironmentInput>())
        {
            var name = NormalizeRequired(environment.Name, "Environment name is required.");
            var profileName = ProjectSecretValidator.ProfileName(environment);
            orderByEnvironment.TryGetValue(name, out var order);
            yield return new ProjectSecretProfilePayload(
                Id: ProfileId(environment),
                EnvironmentId: EnvironmentGroupId(name),
                Name: profileName,
                SortOrder: environment.SortOrder <= 0 ? order : environment.SortOrder);
            orderByEnvironment[name] = order + 1;
        }
    }

    private static IEnumerable<ProjectSecretVariablePayload> NormalizeVariables(IEnumerable<ProjectSecretEnvironmentInput>? environments)
    {
        foreach (var environment in environments ?? Array.Empty<ProjectSecretEnvironmentInput>())
        {
            var profileId = ProfileId(environment);
            foreach (var variable in NormalizeVariablesForProfile(profileId, environment.Variables))
                yield return variable;
        }
    }

    private static IEnumerable<ProjectSecretVariablePayload> NormalizeVariablesForProfile(string profileId, IEnumerable<ProjectSecretVariableInput>? variables)
    {
        var order = 0;
        foreach (var variable in variables ?? Array.Empty<ProjectSecretVariableInput>())
        {
            if (string.IsNullOrWhiteSpace(variable.Key) && string.IsNullOrWhiteSpace(variable.Value))
                continue;

            yield return new ProjectSecretVariablePayload(
                Id: string.IsNullOrWhiteSpace(variable.Id) ? Guid.NewGuid().ToString("N") : variable.Id.Trim(),
                ProfileId: profileId,
                Key: NormalizeRequired(variable.Key, "Variable key is required."),
                Value: variable.Value,
                IsSecret: variable.IsSecret,
                Notes: variable.Notes.Trim(),
                SortOrder: variable.SortOrder <= 0 ? order : variable.SortOrder,
                SourceKind: variable.SourceKind,
                LinkedItemId: variable.LinkedItemId.Trim(),
                LinkedFieldId: variable.LinkedFieldId.Trim(),
                LinkedFieldName: variable.LinkedFieldName.Trim(),
                LastUpdatedAtUtc: string.IsNullOrWhiteSpace(variable.LastUpdatedAtUtc)
                    ? DateTimeOffset.UtcNow.ToString("O")
                    : variable.LastUpdatedAtUtc.Trim());
            order++;
        }
    }

    private static IEnumerable<ProjectSecretLinkedApiKeyPayload> NormalizeLinkedApiKeys(IEnumerable<ProjectSecretLinkedApiKeyInput>? linkedApiKeys)
    {
        foreach (var link in linkedApiKeys ?? Array.Empty<ProjectSecretLinkedApiKeyInput>())
        {
            if (string.IsNullOrWhiteSpace(link.ApiKeyItemId) || string.IsNullOrWhiteSpace(link.ApiKeyFieldId))
                continue;

            yield return new ProjectSecretLinkedApiKeyPayload(
                string.IsNullOrWhiteSpace(link.Id) ? Guid.NewGuid().ToString("N") : link.Id.Trim(),
                link.ApiKeyItemId.Trim(),
                link.ApiKeyFieldId.Trim(),
                link.VariableKey.Trim(),
                link.EnvironmentId.Trim(),
                link.ImportCopy);
        }
    }

    private static ProjectSecretEntry ToEntry(VaultItemHeader header, ProjectSecretPayload payload)
        => new(
            Id: header.Id,
            Name: payload.Name,
            Description: payload.Description,
            Notes: payload.Notes,
            ProjectRootPath: payload.ProjectRootPath,
            Environments: payload.Environments
                .OrderBy(environment => environment.SortOrder)
                .SelectMany(environment => payload.Profiles
                    .Where(profile => string.Equals(profile.EnvironmentId, environment.Id, StringComparison.Ordinal))
                    .OrderBy(profile => profile.SortOrder)
                    .Select(profile => new ProjectSecretEnvironmentEntry(
                        profile.Id,
                        environment.Name,
                        ProfileKind(profile.Name),
                        payload.Variables
                        .Where(variable => string.Equals(variable.ProfileId, profile.Id, StringComparison.Ordinal))
                        .OrderBy(variable => variable.SortOrder)
                        .Select(variable => new ProjectSecretVariableEntry(
                            variable.Id,
                            variable.Key,
                            variable.Value,
                            variable.IsSecret,
                            variable.Notes,
                            variable.SortOrder,
                            variable.SourceKind,
                            variable.LinkedItemId,
                            variable.LinkedFieldId,
                            variable.LinkedFieldName,
                            variable.LastUpdatedAtUtc))
                        .ToArray(),
                        environment.Notes,
                        profile.SortOrder,
                        profile.Name)))
                .ToArray(),
            LinkedApiKeys: payload.LinkedApiKeys
                .Select(link => new ProjectSecretLinkedApiKeyEntry(link.Id, link.ApiKeyItemId, link.ApiKeyFieldId, link.VariableKey, link.EnvironmentId, link.ImportCopy))
                .ToArray(),
            CreatedAtUtc: header.CreatedAtUtc,
            UpdatedAtUtc: header.UpdatedAtUtc,
            LastScanResult: payload.LastScanResult);

    private static ProjectSecretEnvironmentKind ProfileKind(string profileName)
        => Enum.TryParse<ProjectSecretEnvironmentKind>(profileName, true, out var kind)
            ? kind
            : ProjectSecretEnvironmentKind.Development;

    private static string EnvironmentGroupId(string name)
        => $"env_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToUpperInvariant())))[..24].ToLowerInvariant()}";

    private static string ProfileId(ProjectSecretEnvironmentInput environment)
        => string.IsNullOrWhiteSpace(environment.Id)
            ? $"profile_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{environment.Name.Trim().ToUpperInvariant()}|{ProjectSecretValidator.ProfileName(environment).ToUpperInvariant()}")))[..24].ToLowerInvariant()}"
            : environment.Id.Trim();

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException(errorMessage);

        return trimmed;
    }
}
