using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.ProjectSecrets;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed class EnvFileWriter : IProjectSecretEnvWriter
{
    public string WriteEnvironment(IEnumerable<ProjectSecretVariableEntry> variables, Func<ProjectSecretVariableEntry, string> resolveValue)
        => string.Join(Environment.NewLine, variables.Select(variable =>
        {
            var value = resolveValue(variable);
            return $"{variable.Key}={QuoteIfNeeded(value)}";
        })) + Environment.NewLine;

    public string WriteTemplate(IEnumerable<ProjectSecretVariableEntry> variables)
        => string.Join(Environment.NewLine, variables.Select(variable => $"{variable.Key}=")) + Environment.NewLine;

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Any(ch => char.IsWhiteSpace(ch) || ch is '"' or '\'' or '#' or '='))
            return value;

        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }
}
