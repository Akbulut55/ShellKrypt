using ShellKrypt.Application.Items;
using ShellKrypt.Application.ProjectSecrets;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.ProjectSecrets;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class ProjectSecretsTests
{
    private const string MasterPassword = "correct horse battery staple 2026!";

    [Fact]
    public async Task ProjectSecretService_RoundtripsEncryptedProjectAndLinkedApiKeyReferences()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);

        var apiKey = await fixture.ApiKeys.AddAsync(workspace.VaultPath, fixture.VaultKey, new ApiKeyInput(
            "OpenAI",
            "OpenAI",
            "Production",
            "",
            [new ApiKeyFieldInput("token", "API Key", "API Key", "sk-secret-value", true, true, 0)]));

        var saved = await fixture.ProjectSecrets.AddAsync(workspace.VaultPath, fixture.VaultKey, new ProjectSecretInput(
            "MyWebApp",
            "Local app",
            "Notes",
            "/tmp/mywebapp",
            [
                new ProjectSecretEnvironmentInput(
                    "",
                    "Development",
                    ProjectSecretEnvironmentKind.Development,
                    [
                        new ProjectSecretVariableInput("", "DATABASE_URL", "postgres://local", true, "", 0, ProjectSecretVariableSourceKind.Manual, "", "", "", null),
                        new ProjectSecretVariableInput("", "OPENAI_API_KEY", "", true, "", 1, ProjectSecretVariableSourceKind.LinkedApiKey, apiKey.Id, "token", "API Key", null)
                    ],
                    "",
                    0)
            ],
            []));

        var loaded = Assert.Single(await fixture.ProjectSecrets.ListAsync(workspace.VaultPath, fixture.VaultKey));
        Assert.Equal(saved.Id, loaded.Id);
        Assert.Equal("MyWebApp", loaded.Name);
        var variables = Assert.Single(loaded.Environments).Variables;
        Assert.Equal("postgres://local", variables.Single(variable => variable.Key == "DATABASE_URL").Value);
        var linked = variables.Single(variable => variable.Key == "OPENAI_API_KEY");
        Assert.Equal(ProjectSecretVariableSourceKind.LinkedApiKey, linked.SourceKind);
        Assert.Equal("", linked.Value);
        Assert.Equal(apiKey.Id, linked.LinkedItemId);
    }

    [Fact]
    public void EnvParserWriter_HandlesCommonSyntaxWithoutExposingPreviewValues()
    {
        var parsed = EnvFileParser.Parse("""
            # comment
            KEY=value
            export QUOTED="hello world"
            SINGLE='abc'
            SPACED=value with spaces
            KEY=duplicate
            """);

        Assert.Equal(5, parsed.Variables.Count);
        Assert.Contains(parsed.Issues, issue => issue.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("hello world", parsed.Variables.Single(variable => variable.Key == "QUOTED").Value);

        var preview = EnvFileParser.BuildPreview(parsed, ["KEY"]);
        Assert.Contains(preview.Rows, row => row.Key == "KEY" && row.Status == ProjectSecretEnvRowStatus.Conflict);
        Assert.All(preview.Rows, row => Assert.Equal("", row.Value));

        var envText = EnvFileWriter.WriteEnvironment([
            new ProjectSecretVariableEntry("1", "SPACED", "hello world", true, "", 0, ProjectSecretVariableSourceKind.Manual, "", "", "", null)
        ]);
        Assert.Contains("SPACED=\"hello world\"", envText);
        Assert.Equal("SPACED=\n", EnvFileWriter.WriteTemplate([
            new ProjectSecretVariableEntry("1", "SPACED", "hello world", true, "", 0, ProjectSecretVariableSourceKind.Manual, "", "", "", null)
        ]).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_DetectsMissingEmptyInvalidAndDifferentWithoutRawValues()
    {
        var project = new ProjectSecretEntry(
            "project",
            "Project",
            "",
            "",
            null,
            [
                new ProjectSecretEnvironmentEntry(
                    "dev",
                    "Development",
                    ProjectSecretEnvironmentKind.Development,
                    [
                        new ProjectSecretVariableEntry("1", "DATABASE_URL", "secret-dev", true, "", 0, ProjectSecretVariableSourceKind.Manual, "", "", "", null),
                        new ProjectSecretVariableEntry("2", "EMPTY_SECRET", "", true, "", 1, ProjectSecretVariableSourceKind.Manual, "", "", "", null),
                        new ProjectSecretVariableEntry("3", "BAD-KEY", "value", true, "", 2, ProjectSecretVariableSourceKind.Manual, "", "", "", null)
                    ],
                    "",
                    0),
                new ProjectSecretEnvironmentEntry(
                    "prod",
                    "Production",
                    ProjectSecretEnvironmentKind.Production,
                    [
                        new ProjectSecretVariableEntry("4", "DATABASE_URL", "secret-prod", true, "", 0, ProjectSecretVariableSourceKind.Manual, "", "", "", null)
                    ],
                    "",
                    1)
            ],
            [],
            "",
            "");

        var result = ProjectSecretComparer.Compare(project);
        Assert.Contains(result.Rows, row => row.VariableKey == "DATABASE_URL" && row.Cells.All(cell => cell.Status == ProjectSecretCompareStatus.Different));
        Assert.Contains(result.Rows, row => row.VariableKey == "EMPTY_SECRET" && row.Cells.Any(cell => cell.Status == ProjectSecretCompareStatus.Empty));
        Assert.Contains(result.Rows, row => row.VariableKey == "BAD-KEY" && row.Cells.Any(cell => cell.Status == ProjectSecretCompareStatus.InvalidKey));
        var visible = string.Join("\n", result.Rows.Select(row => $"{row.VariableKey} {string.Join(' ', row.Cells.Select(cell => cell.Status))}"));
        Assert.DoesNotContain("secret-dev", visible);
        Assert.DoesNotContain("secret-prod", visible);
    }

    [Fact]
    public void Scanner_DetectsUsageMissingEnvFileAndLeaksWithoutReturningSecretValues()
    {
        using var workspace = new TempWorkspace();
        var projectRoot = workspace.DirectoryPath("project");
        Directory.CreateDirectory(projectRoot);
        File.WriteAllText(Path.Combine(projectRoot, "app.js"), "const url = process.env.DATABASE_URL;\nconst token = \"super-secret-value-2026\";\nconst missing = process.env.REDIS_URL;");
        File.WriteAllText(Path.Combine(projectRoot, ".env"), "DATABASE_URL=postgres://local\n");
        Directory.CreateDirectory(Path.Combine(projectRoot, "node_modules"));
        File.WriteAllText(Path.Combine(projectRoot, "node_modules", "ignored.js"), "OLD_SECRET");

        var result = new ProjectSecretFilesystemScanner().Scan(new ProjectSecretScanRequest(
            "project",
            projectRoot,
            ["DATABASE_URL", "OLD_SECRET"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["API_TOKEN"] = "super-secret-value-2026"
            }));

        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.EnvFileWithValuesDetected);
        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.ReferencedButMissingVariable && finding.VariableKey == "REDIS_URL");
        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.UnusedVariable && finding.VariableKey == "OLD_SECRET");
        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.PossiblePlaintextLeak && finding.VariableKey == "API_TOKEN");
        Assert.DoesNotContain("super-secret-value-2026", string.Join("\n", result.Findings.Select(finding => finding.Message)));
    }

    [Fact]
    public async Task AuditAndSummaries_IncludeProjectSecretsWithoutExposingValues()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);

        await fixture.ProjectSecrets.AddAsync(workspace.VaultPath, fixture.VaultKey, new ProjectSecretInput(
            "AuditProject",
            "",
            "",
            null,
            [
                new ProjectSecretEnvironmentInput(
                    "",
                    "Development",
                    ProjectSecretEnvironmentKind.Development,
                    [
                        new ProjectSecretVariableInput("", "EMPTY_SECRET", "", true, "", 0, ProjectSecretVariableSourceKind.Manual, "", "", "", null),
                        new ProjectSecretVariableInput("", "BAD-KEY", "raw-secret-value", true, "", 1, ProjectSecretVariableSourceKind.Manual, "", "", "", null),
                        new ProjectSecretVariableInput("", "LINKED_SECRET", "", true, "", 2, ProjectSecretVariableSourceKind.LinkedApiKey, "missing", "field", "API Key", null)
                    ],
                    "",
                    0),
                new ProjectSecretEnvironmentInput("", "Production", ProjectSecretEnvironmentKind.Production, [], "", 1)
            ],
            []));

        var audit = await fixture.Audit.AnalyzeAsync(workspace.VaultPath, fixture.VaultKey);
        Assert.True(audit.ProjectSecretIssueCount > 0);
        Assert.Contains(audit.Issues, issue => issue.Category == HealthAuditCategory.ProjectSecretEmptyValue);
        Assert.Contains(audit.Issues, issue => issue.Category == HealthAuditCategory.ProjectSecretInvalidKey);
        Assert.Contains(audit.Issues, issue => issue.Category == HealthAuditCategory.ProjectSecretBrokenApiKeyLink);
        Assert.All(audit.Issues, issue => Assert.DoesNotContain("raw-secret-value", issue.Details));

        var summaries = await fixture.Summaries.ListAsync(workspace.VaultPath, fixture.VaultKey, ItemListQuery.Default(20));
        Assert.Equal(1, summaries.Counts.ProjectSecrets);
        Assert.Contains(summaries.AllItems, item => item.Type == ItemType.ProjectSecret && item.Title == "AuditProject");
        Assert.DoesNotContain(summaries.AllItems, item => item.SearchText.Contains("raw-secret-value", StringComparison.Ordinal));
    }

    private static async Task<Fixture> CreateUnlockedFixtureAsync(string vaultPath)
    {
        var vaultService = new SqliteVaultService();
        var itemRepository = new SqliteItemRepository();
        await vaultService.CreateAsync(vaultPath, MasterPassword);
        var unlock = await vaultService.UnlockAsync(vaultPath, MasterPassword);
        Assert.True(unlock.Success);

        return new Fixture(
            unlock.VaultKey!,
            new ApiKeyService(itemRepository),
            new ProjectSecretService(itemRepository),
            new HealthAuditService(itemRepository),
            new VaultItemSummaryService(itemRepository, new VaultItemPayloadReader()));
    }

    private sealed record Fixture(
        byte[] VaultKey,
        ApiKeyService ApiKeys,
        ProjectSecretService ProjectSecrets,
        HealthAuditService Audit,
        VaultItemSummaryService Summaries);

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "shellkrypt-project-secrets-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }
        public string VaultPath => FilePath("vault.skvault");
        public string FilePath(string fileName) => Path.Combine(Root, fileName);
        public string DirectoryPath(string directoryName) => Path.Combine(Root, directoryName);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
