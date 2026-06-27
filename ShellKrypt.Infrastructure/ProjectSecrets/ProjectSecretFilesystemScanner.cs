using System.Text.RegularExpressions;
using ShellKrypt.Core.Items;

namespace ShellKrypt.Infrastructure.ProjectSecrets;

public sealed class ProjectSecretFilesystemScanner
{
    private static readonly Regex EnvReferencePattern = new(
        @"(?:(?:process\.env|import\.meta\.env)\.|\$env:|\$\{|%|GetEnvironmentVariable\(""|getenv\(""|System\.getenv\("")([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    private static readonly Regex SecretLikeAssignmentPattern = new(
        @"(?i)(secret|token|api[_-]?key|password)\s*[:=]\s*[""']?[^""'\s]{16,}",
        RegexOptions.Compiled);

    public ProjectSecretScanResult Scan(ProjectSecretScanRequest request)
    {
        var started = DateTimeOffset.UtcNow;
        var findings = new List<ProjectSecretScanFinding>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        var variableKeys = new HashSet<string>(request.VariableKeys.Where(key => !string.IsNullOrWhiteSpace(key)), StringComparer.Ordinal);
        var secretValues = request.SecretValues
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value) && pair.Value.Length >= 8)
            .ToArray();

        var filesScanned = 0;
        var filesSkipped = 0;
        var bytesScanned = 0L;

        if (string.IsNullOrWhiteSpace(request.ProjectRootPath) || !Directory.Exists(request.ProjectRootPath))
        {
            AddFinding(findings, request, ProjectSecretScanFindingKind.BrokenProjectRoot, HealthAuditSeverity.Medium, null, null, null, null, "Project root is missing or unavailable.");
            return Complete(request, started, findings, filesScanned, filesSkipped, bytesScanned);
        }

        foreach (var path in EnumerateFilesSafe(request.ProjectRootPath))
        {
            if (findings.Count >= ProjectSecretScannerLimits.MaxFindings)
                break;

            if (filesScanned >= ProjectSecretScannerLimits.MaxFilesScanned || bytesScanned >= ProjectSecretScannerLimits.MaxTotalBytesScanned)
            {
                AddFinding(findings, request, ProjectSecretScanFindingKind.ScanLimitReached, HealthAuditSeverity.Low, null, null, null, null, "Project scan limit reached.");
                break;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch
            {
                filesSkipped++;
                continue;
            }

            if (!ProjectSecretScanRules.ShouldScanFile(path) || info.Length > ProjectSecretScannerLimits.MaxFileSizeBytes)
            {
                filesSkipped++;
                if (info.Length > ProjectSecretScannerLimits.MaxFileSizeBytes)
                    AddFinding(findings, request, ProjectSecretScanFindingKind.SkippedLargeFile, HealthAuditSeverity.Low, null, RelativePath(request.ProjectRootPath, path), null, null, "Skipped large file.");
                continue;
            }

            string text;
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (IsLikelyBinary(bytes))
                {
                    filesSkipped++;
                    continue;
                }

                text = System.Text.Encoding.UTF8.GetString(bytes);
                bytesScanned += bytes.Length;
            }
            catch
            {
                filesSkipped++;
                continue;
            }

            filesScanned++;
            ScanFile(request, path, text, variableKeys, secretValues, used, referenced, findings);
        }

        foreach (var key in variableKeys.Except(used, StringComparer.Ordinal))
            AddFinding(findings, request, ProjectSecretScanFindingKind.UnusedVariable, HealthAuditSeverity.Low, key, null, null, key, $"{key} was not referenced in scanned project files.");

        foreach (var key in referenced.Except(variableKeys, StringComparer.Ordinal))
            AddFinding(findings, request, ProjectSecretScanFindingKind.ReferencedButMissingVariable, HealthAuditSeverity.Medium, key, null, null, key, $"{key} is referenced in project files but is not stored in Project Secrets.");

        return Complete(request, started, findings, filesScanned, filesSkipped, bytesScanned);
    }

    private static void ScanFile(
        ProjectSecretScanRequest request,
        string path,
        string text,
        HashSet<string> variableKeys,
        KeyValuePair<string, string>[] secretValues,
        HashSet<string> used,
        HashSet<string> referenced,
        List<ProjectSecretScanFinding> findings)
    {
        var relative = RelativePath(request.ProjectRootPath, path);
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var envFileHasValues = Path.GetFileName(path).StartsWith(".env", StringComparison.OrdinalIgnoreCase)
                               && lines.Any(line => LooksLikeEnvValueLine(line));

        if (envFileHasValues)
            AddFinding(findings, request, ProjectSecretScanFindingKind.EnvFileWithValuesDetected, HealthAuditSeverity.Medium, null, relative, null, null, "A .env file with values exists in the project folder.");

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (Match match in EnvReferencePattern.Matches(line))
            {
                var key = match.Groups[1].Value.Trim('%', '}');
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                referenced.Add(key);
                if (variableKeys.Contains(key))
                    used.Add(key);
            }

            foreach (var key in variableKeys)
            {
                if (ContainsKeyReference(line, key))
                    used.Add(key);
            }

            foreach (var pair in secretValues)
            {
                if (!line.Contains(pair.Value, StringComparison.Ordinal))
                    continue;

                AddFinding(findings, request, ProjectSecretScanFindingKind.PossiblePlaintextLeak, HealthAuditSeverity.High, pair.Key, relative, i + 1, pair.Key, $"Possible hardcoded value for {pair.Key} found.");
                break;
            }

            if (SecretLikeAssignmentPattern.IsMatch(line))
                AddFinding(findings, request, ProjectSecretScanFindingKind.PossiblePlaintextLeak, HealthAuditSeverity.Medium, null, relative, i + 1, null, "Possible hardcoded secret-like assignment found.");
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            IEnumerable<string> directories = Array.Empty<string>();
            IEnumerable<string> files = Array.Empty<string>();

            try
            {
                directories = Directory.EnumerateDirectories(current);
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                // Ignore inaccessible folders; the scanner reports aggregate skipped files only.
            }

            foreach (var directory in directories)
            {
                if (!ProjectSecretScanRules.ShouldSkipDirectory(directory))
                    stack.Push(directory);
            }

            foreach (var file in files)
                yield return file;
        }
    }

    private static ProjectSecretScanResult Complete(ProjectSecretScanRequest request, DateTimeOffset started, IReadOnlyList<ProjectSecretScanFinding> findings, int filesScanned, int filesSkipped, long bytesScanned)
        => new(request.ProjectId, request.ProjectRootPath, started.ToString("O"), DateTimeOffset.UtcNow.ToString("O"), filesScanned, filesSkipped, bytesScanned, findings.Take(ProjectSecretScannerLimits.MaxFindings).ToArray());

    private static void AddFinding(List<ProjectSecretScanFinding> findings, ProjectSecretScanRequest request, ProjectSecretScanFindingKind kind, HealthAuditSeverity severity, string? key, string? relativePath, int? lineNumber, string? variableKey, string message)
    {
        if (findings.Count >= ProjectSecretScannerLimits.MaxFindings)
            return;

        findings.Add(new ProjectSecretScanFinding(kind, severity, request.ProjectId, null, null, variableKey ?? key, relativePath, lineNumber, message));
    }

    private static bool ContainsKeyReference(string line, string key)
        => Regex.IsMatch(line, $@"(?<![A-Za-z0-9_]){Regex.Escape(key)}(?![A-Za-z0-9_])");

    private static bool LooksLikeEnvValueLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0 && !trimmed.StartsWith('#') && trimmed.Contains('=') && !trimmed.EndsWith('=');
    }

    private static bool IsLikelyBinary(byte[] bytes)
        => bytes.Take(Math.Min(bytes.Length, 1024)).Any(value => value == 0);

    private static string RelativePath(string root, string path)
    {
        try
        {
            return Path.GetRelativePath(root, path);
        }
        catch
        {
            return Path.GetFileName(path);
        }
    }
}
