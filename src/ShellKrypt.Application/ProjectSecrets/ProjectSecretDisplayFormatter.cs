using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Application.ProjectSecrets;

public static class ProjectSecretDisplayFormatter
{
    public static string MaskValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Length <= 4
            ? new string('*', value.Length)
            : $"{value[..2]}{new string('*', Math.Min(8, value.Length - 4))}{value[^2..]}";
    }

    public static string SourceLabel(ProjectSecretVariableSourceKind sourceKind)
        => sourceKind switch
        {
            ProjectSecretVariableSourceKind.ReferencedApiKey => "Referenced API Key",
            ProjectSecretVariableSourceKind.ImportedApiKey => "Imported API Key",
            ProjectSecretVariableSourceKind.ImportedEnvFile => "Imported .env",
            _ => "Manual"
        };

    public static string CompareStatusLabel(ProjectSecretCompareStatus status)
        => status switch
        {
            ProjectSecretCompareStatus.Missing => "missing",
            ProjectSecretCompareStatus.Empty => "empty",
            ProjectSecretCompareStatus.InvalidKey => "invalid",
            ProjectSecretCompareStatus.Different => "different",
            ProjectSecretCompareStatus.BrokenReference => "broken reference",
            _ => "present"
        };
}
