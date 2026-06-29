using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

internal static class ProjectSecretPayloadCompatibility
{
    public static ProjectSecretPayload Deserialize(byte[] json, JsonSerializerOptions options)
    {
        var payload = JsonSerializer.Deserialize<ProjectSecretPayload>(json, options);
        if (payload is not null && payload.Profiles is not null && payload.Variables is not null && payload.LinkedApiKeys is not null)
            return Normalize(payload);

        var legacy = JsonSerializer.Deserialize<LegacyProjectSecretPayload>(json, options);
        return legacy is null
            ? Empty()
            : FromLegacy(legacy);
    }

    public static ProjectSecretPayload Empty()
        => new(
            "",
            "",
            "",
            null,
            Array.Empty<ProjectSecretEnvironmentPayload>(),
            Array.Empty<ProjectSecretProfilePayload>(),
            Array.Empty<ProjectSecretVariablePayload>(),
            Array.Empty<ProjectSecretLinkedApiKeyPayload>());

    private static ProjectSecretPayload Normalize(ProjectSecretPayload payload)
        => new(
            payload.Name ?? "",
            payload.Description ?? "",
            payload.Notes ?? "",
            string.IsNullOrWhiteSpace(payload.ProjectRootPath) ? null : payload.ProjectRootPath,
            payload.Environments ?? Array.Empty<ProjectSecretEnvironmentPayload>(),
            payload.Profiles ?? Array.Empty<ProjectSecretProfilePayload>(),
            payload.Variables ?? Array.Empty<ProjectSecretVariablePayload>(),
            payload.LinkedApiKeys ?? Array.Empty<ProjectSecretLinkedApiKeyPayload>(),
            payload.LastScanResult);

    private static ProjectSecretPayload FromLegacy(LegacyProjectSecretPayload legacy)
    {
        var environmentGroups = new List<ProjectSecretEnvironmentPayload>();
        var profiles = new List<ProjectSecretProfilePayload>();
        var variables = new List<ProjectSecretVariablePayload>();
        var grouped = (legacy.Environments ?? Array.Empty<LegacyProjectSecretEnvironmentPayload>())
            .Where(environment => !string.IsNullOrWhiteSpace(environment.Name))
            .GroupBy(environment => environment.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        var environmentOrder = 0;
        foreach (var group in grouped)
        {
            var first = group.First();
            var environmentId = EnvironmentGroupId(group.Key);
            environmentGroups.Add(new ProjectSecretEnvironmentPayload(
                environmentId,
                group.Key,
                first.Notes ?? "",
                first.SortOrder <= 0 ? environmentOrder : first.SortOrder));

            var profileOrder = 0;
            foreach (var environment in group)
            {
                var profileId = string.IsNullOrWhiteSpace(environment.Id)
                    ? ProfileId(group.Key, environment.Kind)
                    : environment.Id.Trim();
                profiles.Add(new ProjectSecretProfilePayload(
                    profileId,
                    environmentId,
                    string.IsNullOrWhiteSpace(environment.ProfileName) ? environment.Kind.ToString() : environment.ProfileName.Trim(),
                    environment.SortOrder <= 0 ? profileOrder : environment.SortOrder));

                foreach (var variable in environment.Variables ?? Array.Empty<LegacyProjectSecretVariablePayload>())
                {
                    variables.Add(new ProjectSecretVariablePayload(
                        variable.Id ?? "",
                        profileId,
                        variable.Key ?? "",
                        variable.Value ?? "",
                        variable.IsSecret,
                        variable.Notes ?? "",
                        variable.SortOrder,
                        variable.SourceKind,
                        variable.LinkedItemId ?? "",
                        variable.LinkedFieldId ?? "",
                        variable.LinkedFieldName ?? "",
                        variable.LastUpdatedAtUtc));
                }

                profileOrder++;
            }

            environmentOrder++;
        }

        return new ProjectSecretPayload(
            legacy.Name ?? "",
            legacy.Description ?? "",
            legacy.Notes ?? "",
            string.IsNullOrWhiteSpace(legacy.ProjectRootPath) ? null : legacy.ProjectRootPath,
            environmentGroups,
            profiles,
            variables,
            legacy.LinkedApiKeys ?? Array.Empty<ProjectSecretLinkedApiKeyPayload>(),
            legacy.LastScanResult);
    }

    private static string EnvironmentGroupId(string name)
        => $"env_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToUpperInvariant())))[..24].ToLowerInvariant()}";

    private static string ProfileId(string environmentName, ProjectSecretEnvironmentKind kind)
        => $"profile_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{environmentName.Trim().ToUpperInvariant()}|{kind}")))[..24].ToLowerInvariant()}";

    private sealed record LegacyProjectSecretPayload(
        string? Name,
        string? Description,
        string? Notes,
        string? ProjectRootPath,
        IReadOnlyList<LegacyProjectSecretEnvironmentPayload>? Environments,
        IReadOnlyList<ProjectSecretLinkedApiKeyPayload>? LinkedApiKeys,
        ProjectSecretScanResult? LastScanResult = null);

    private sealed record LegacyProjectSecretEnvironmentPayload(
        string Id,
        string Name,
        ProjectSecretEnvironmentKind Kind,
        IReadOnlyList<LegacyProjectSecretVariablePayload>? Variables,
        string? Notes,
        int SortOrder,
        string? ProfileName = "");

    private sealed record LegacyProjectSecretVariablePayload(
        string? Id,
        string? Key,
        string? Value,
        bool IsSecret,
        string? Notes,
        int SortOrder,
        ProjectSecretVariableSourceKind SourceKind,
        string? LinkedItemId,
        string? LinkedFieldId,
        string? LinkedFieldName,
        string? LastUpdatedAtUtc);
}
