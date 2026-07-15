using ShellKrypt.Application.ProjectSecrets;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed class EnvFileParser : IProjectSecretEnvParser
{
    public ProjectSecretEnvParseResult Parse(string text)
    {
        var variables = new List<ProjectSecretEnvVariable>();
        var issues = new List<ProjectSecretEnvParseIssue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var rawLine = lines[i];
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var equals = line.IndexOf('=');
            if (equals <= 0)
            {
                issues.Add(new ProjectSecretEnvParseIssue(lineNumber, "Malformed .env line."));
                continue;
            }

            var key = line[..equals].Trim();
            var value = Unquote(line[(equals + 1)..].Trim());
            if (key.Length == 0)
            {
                issues.Add(new ProjectSecretEnvParseIssue(lineNumber, "Empty .env key."));
                continue;
            }

            if (!seen.Add(key))
                issues.Add(new ProjectSecretEnvParseIssue(lineNumber, $"Duplicate key {key}."));

            variables.Add(new ProjectSecretEnvVariable(key, value, lineNumber));
        }

        return new ProjectSecretEnvParseResult(variables, issues);
    }

    public ProjectSecretEnvImportPreview BuildPreview(
        ProjectSecretEnvParseResult parseResult,
        IReadOnlyCollection<string> existingKeys)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var existing = new HashSet<string>(existingKeys, StringComparer.Ordinal);
        var rows = new List<ProjectSecretEnvImportPreviewRow>();

        foreach (var variable in parseResult.Variables)
        {
            var status = ProjectSecretEnvRowStatus.New;
            if (string.IsNullOrWhiteSpace(variable.Key))
                status = ProjectSecretEnvRowStatus.Invalid;
            else if (!seen.Add(variable.Key))
                status = ProjectSecretEnvRowStatus.Duplicate;
            else if (existing.Contains(variable.Key))
                status = ProjectSecretEnvRowStatus.Conflict;

            rows.Add(new ProjectSecretEnvImportPreviewRow(variable.LineNumber, variable.Key, "", status, BuildMessage(status, variable.Value.Length)));
        }

        return new ProjectSecretEnvImportPreview(
            rows.Count,
            rows.Count(row => row.Status == ProjectSecretEnvRowStatus.New),
            rows.Count(row => row.Status == ProjectSecretEnvRowStatus.Conflict),
            rows.Count(row => row.Status == ProjectSecretEnvRowStatus.Duplicate),
            rows.Count(row => row.Status == ProjectSecretEnvRowStatus.Invalid),
            rows);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1]
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);

        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            return value[1..^1];

        return value;
    }

    private static string BuildMessage(ProjectSecretEnvRowStatus status, int valueLength)
        => status switch
        {
            ProjectSecretEnvRowStatus.Conflict => $"Existing key, {valueLength} character value.",
            ProjectSecretEnvRowStatus.Duplicate => $"Duplicate key, {valueLength} character value.",
            ProjectSecretEnvRowStatus.Invalid => "Invalid key.",
            _ => $"New key, {valueLength} character value."
        };
}
