using ShellKrypt.Core.Items;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Application.ProjectSecrets;

public interface IProjectSecretValueResolver
{
    string? Resolve(ProjectSecretVariableEntry variable, IReadOnlyList<ApiKeyEntry> apiKeys);
}

public sealed class ProjectSecretValueResolver : IProjectSecretValueResolver
{
    public string? Resolve(ProjectSecretVariableEntry variable, IReadOnlyList<ApiKeyEntry> apiKeys)
    {
        if (variable.SourceKind != ProjectSecretVariableSourceKind.ReferencedApiKey)
            return variable.Value;

        var apiKey = apiKeys.FirstOrDefault(item => string.Equals(item.Id, variable.ReferencedItemId, StringComparison.Ordinal));
        var field = apiKey?.Fields.FirstOrDefault(item => string.Equals(item.Id, variable.ReferencedFieldId, StringComparison.Ordinal));
        return field?.Value;
    }
}
