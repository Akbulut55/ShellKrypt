using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.Items;

public sealed partial class ProjectSecretService
{
    public static string ResolveLinkedApiKeyValue(
        IReadOnlyList<ApiKeyEntry> apiKeys,
        ProjectSecretVariableEntry variable)
    {
        if (variable.SourceKind != ProjectSecretVariableSourceKind.LinkedApiKey)
            return variable.Value;

        var apiKey = apiKeys.FirstOrDefault(item => string.Equals(item.Id, variable.LinkedItemId, StringComparison.Ordinal));
        var field = apiKey?.Fields.FirstOrDefault(item => string.Equals(item.Id, variable.LinkedFieldId, StringComparison.Ordinal));
        return field?.Value ?? string.Empty;
    }
}
