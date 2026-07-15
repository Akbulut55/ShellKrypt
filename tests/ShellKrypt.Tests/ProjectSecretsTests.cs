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
    public async Task Service_RoundtripsNestedHierarchyAndReferenceWithoutValueCopy()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);
        var apiKey = await fixture.ApiKeys.AddAsync(workspace.VaultPath, fixture.VaultKey, new ApiKeyInput(
            "OpenAI", "OpenAI", "user", "", [new ApiKeyFieldInput("token", "API Key", "API Key", "sk-secret-value", true, true, 0)]));

        var saved = await fixture.ProjectSecrets.AddAsync(workspace.VaultPath, fixture.VaultKey, Project(
            "MyWebApp",
            [Environment("backend", "Backend", [Profile("development", "Development", [
                Variable("database", "DATABASE_URL", "postgres://local"),
                Variable("openai", "OPENAI_API_KEY", "", ProjectSecretVariableSourceKind.ReferencedApiKey, apiKey.Id, "token", "API Key")
            ])])]));

        var loaded = Assert.Single(await fixture.ProjectSecrets.ListAsync(workspace.VaultPath, fixture.VaultKey));
        Assert.Equal(saved.Id, loaded.Id);
        var profile = Assert.Single(Assert.Single(loaded.Environments).Profiles);
        Assert.Equal("postgres://local", profile.Variables.Single(variable => variable.Key == "DATABASE_URL").Value);
        var referenced = profile.Variables.Single(variable => variable.Key == "OPENAI_API_KEY");
        Assert.Equal(ProjectSecretVariableSourceKind.ReferencedApiKey, referenced.SourceKind);
        Assert.Empty(referenced.Value);
        Assert.Equal(apiKey.Id, referenced.ReferencedItemId);
        Assert.Equal("token", referenced.ReferencedFieldId);
    }

    [Fact]
    public async Task Service_StoresImportedApiKeyCopyIndependentlyWithoutReferenceMetadata()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);
        const string copiedValue = "copied-api-key-value";

        await fixture.ProjectSecrets.AddAsync(workspace.VaultPath, fixture.VaultKey, Project(
            "ImportedKeyProject",
            [Environment("backend", "Backend", [Profile("development", "Development", [
                Variable("api", "SERVICE_API_KEY", copiedValue, ProjectSecretVariableSourceKind.ImportedApiKey,
                    "source-item", "source-field", "API Key")
            ])])]));

        var loaded = Assert.Single(await fixture.ProjectSecrets.ListAsync(workspace.VaultPath, fixture.VaultKey));
        var imported = Assert.Single(Assert.Single(Assert.Single(loaded.Environments).Profiles).Variables);
        Assert.Equal(ProjectSecretVariableSourceKind.ImportedApiKey, imported.SourceKind);
        Assert.Equal(copiedValue, imported.Value);
        Assert.Empty(imported.ReferencedItemId);
        Assert.Empty(imported.ReferencedFieldId);
        Assert.Empty(imported.ReferencedFieldName);
    }

    [Fact]
    public void ValueResolver_ReferencesCurrentApiKeyValueButImportedCopyRemainsIndependent()
    {
        var resolver = new ProjectSecretValueResolver();
        var referenced = ToEntry(Variable("reference", "SERVICE_API_KEY", "", ProjectSecretVariableSourceKind.ReferencedApiKey,
            "api-key", "field", "API Key"));
        var imported = ToEntry(Variable("import", "SERVICE_API_KEY_COPY", "copied-value", ProjectSecretVariableSourceKind.ImportedApiKey));
        var original = ApiKey("original-value");
        var updated = ApiKey("updated-value");

        Assert.Equal("original-value", resolver.Resolve(referenced, [original]));
        Assert.Equal("updated-value", resolver.Resolve(referenced, [updated]));
        Assert.Equal("copied-value", resolver.Resolve(imported, [updated]));
    }

    [Fact]
    public async Task Service_AllowsEmptyHierarchyAndRejectsDuplicateProjectNames()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);
        await fixture.ProjectSecrets.AddAsync(workspace.VaultPath, fixture.VaultKey, Project("EmptyProject", []));
        var loaded = Assert.Single(await fixture.ProjectSecrets.ListAsync(workspace.VaultPath, fixture.VaultKey));
        Assert.Empty(loaded.Environments);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ProjectSecrets.AddAsync(workspace.VaultPath, fixture.VaultKey, Project("emptyproject", [])));
    }

    [Fact]
    public void EnvParserWriter_HandlesCommonSyntaxWithoutExposingPreviewValues()
    {
        var parser = new EnvFileParser();
        var writer = new EnvFileWriter();
        var parsed = parser.Parse("# comment\nKEY=value\nexport QUOTED=\"hello world\"\nSINGLE='abc'\nSPACED=value with spaces\nKEY=duplicate\n");
        Assert.Equal(5, parsed.Variables.Count);
        Assert.Contains(parsed.Issues, issue => issue.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("hello world", parsed.Variables.Single(variable => variable.Key == "QUOTED").Value);
        var preview = parser.BuildPreview(parsed, ["KEY"]);
        Assert.Contains(preview.Rows, row => row.Key == "KEY" && row.Status == ProjectSecretEnvRowStatus.Conflict);
        Assert.All(preview.Rows, row => Assert.Empty(row.Value));

        var variable = ToEntry(Variable("spaced", "SPACED", "hello world"));
        Assert.Contains("SPACED=\"hello world\"", writer.WriteEnvironment([variable], item => item.Value));
        Assert.Equal("SPACED=\n", writer.WriteTemplate([variable]).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_OnlyComparesProfilesWithinEnvironmentAndNeverReturnsValues()
    {
        var environment = new ProjectSecretEnvironmentEntry("backend", "Backend", "", 0, [
            ToEntry(Profile("dev", "Development", [Variable("1", "DATABASE_URL", "secret-dev"), Variable("2", "EMPTY_SECRET", ""), Variable("3", "BAD-KEY", "value")])),
            ToEntry(Profile("prod", "Production", [Variable("4", "DATABASE_URL", "secret-prod")]))
        ]);
        var result = ProjectSecretComparer.Compare(environment);
        Assert.Contains(result.Rows, row => row.VariableKey == "DATABASE_URL" && row.Cells.All(cell => cell.Status == ProjectSecretCompareStatus.Different));
        Assert.Contains(result.Rows, row => row.VariableKey == "EMPTY_SECRET" && row.Cells.Any(cell => cell.Status == ProjectSecretCompareStatus.Empty));
        Assert.Contains(result.Rows, row => row.VariableKey == "BAD-KEY" && row.Cells.Any(cell => cell.Status == ProjectSecretCompareStatus.InvalidKey));
        var visible = string.Join('\n', result.Rows.Select(row => $"{row.VariableKey} {string.Join(' ', row.Cells.Select(cell => cell.Status))}"));
        Assert.DoesNotContain("secret-dev", visible);
        Assert.DoesNotContain("secret-prod", visible);
    }

    [Fact]
    public async Task Scanner_IsProfileScopedAndDoesNotReturnSecretValues()
    {
        using var workspace = new TempWorkspace();
        var root = workspace.DirectoryPath("project");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "app.js"), "const url = process.env.DATABASE_URL;\nconst token = \"super-secret-value-2026\";\nconst missing = process.env.REDIS_URL;");
        await File.WriteAllTextAsync(Path.Combine(root, ".env"), "DATABASE_URL=postgres://local\n");
        Directory.CreateDirectory(Path.Combine(root, "node_modules"));
        await File.WriteAllTextAsync(Path.Combine(root, "node_modules", "ignored.js"), "OLD_SECRET");

        var result = await new ProjectSecretFilesystemScanner().ScanAsync(new ProjectSecretScanRequest(
            "project", "backend", "development", root, ["DATABASE_URL", "OLD_SECRET"],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["API_TOKEN"] = "super-secret-value-2026" }));
        Assert.Equal("backend", result.EnvironmentId);
        Assert.Equal("development", result.ProfileId);
        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.EnvFileWithValuesDetected);
        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.ReferencedButMissingVariable && finding.VariableKey == "REDIS_URL");
        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.UnusedVariable && finding.VariableKey == "OLD_SECRET");
        Assert.Contains(result.Findings, finding => finding.Kind == ProjectSecretScanFindingKind.PossiblePlaintextLeak && finding.VariableKey == "API_TOKEN");
        Assert.DoesNotContain("super-secret-value-2026", string.Join('\n', result.Findings.Select(finding => finding.Message)));
    }

    [Fact]
    public async Task AuditAndSummaries_UseNestedHierarchyWithoutExposingValues()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);
        await fixture.ProjectSecrets.AddAsync(workspace.VaultPath, fixture.VaultKey, Project("AuditProject", [
            Environment("backend", "Backend", [Profile("production", "Production", [
                Variable("empty", "EMPTY_SECRET", ""),
                Variable("invalid", "BAD-KEY", "raw-secret-value"),
                Variable("broken", "REFERENCED_SECRET", "", ProjectSecretVariableSourceKind.ReferencedApiKey, "missing", "field", "API Key")
            ])])
        ]));
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

    private static ProjectSecretInput Project(string name, IReadOnlyList<ProjectSecretEnvironmentInput> environments)
        => new(name, "", "", null, environments, []);
    private static ProjectSecretEnvironmentInput Environment(string id, string name, IReadOnlyList<ProjectSecretProfileInput> profiles)
        => new(id, name, "", 0, profiles);
    private static ProjectSecretProfileInput Profile(string id, string name, IReadOnlyList<ProjectSecretVariableInput> variables)
        => new(id, name, 0, variables);
    private static ProjectSecretVariableInput Variable(string id, string key, string value, ProjectSecretVariableSourceKind source = ProjectSecretVariableSourceKind.Manual, string itemId = "", string fieldId = "", string fieldName = "")
        => new(id, key, value, true, "", 0, source, itemId, fieldId, fieldName, null);
    private static ProjectSecretVariableEntry ToEntry(ProjectSecretVariableInput value)
        => new(value.Id, value.Key, value.Value, value.IsSecret, value.Notes, value.SortOrder, value.SourceKind, value.ReferencedItemId, value.ReferencedFieldId, value.ReferencedFieldName, value.LastUpdatedAtUtc);
    private static ProjectSecretProfileEntry ToEntry(ProjectSecretProfileInput value)
        => new(value.Id, value.Name, value.SortOrder, value.Variables.Select(ToEntry).ToArray());
    private static ApiKeyEntry ApiKey(string value)
        => new("api-key", "Service", "Provider", "Production", "", [new ApiKeyFieldEntry("field", "API Key", "API Key", value, true, true, 0)], "", "", "user");

    private static async Task<Fixture> CreateUnlockedFixtureAsync(string vaultPath)
    {
        var vaultService = new SqliteVaultService();
        var repository = new SqliteItemRepository();
        await vaultService.CreateAsync(vaultPath, MasterPassword);
        var unlock = await vaultService.UnlockAsync(vaultPath, MasterPassword);
        Assert.True(unlock.Success);
        return new(unlock.VaultKey!, new ApiKeyService(repository), new ProjectSecretService(repository), new HealthAuditService(repository), new VaultItemSummaryService(repository, new VaultItemPayloadReader()));
    }

    private sealed record Fixture(byte[] VaultKey, ApiKeyService ApiKeys, ProjectSecretService ProjectSecrets, HealthAuditService Audit, VaultItemSummaryService Summaries);

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace() { Root = Path.Combine(Path.GetTempPath(), "shellkrypt-project-secrets-tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root); }
        public string Root { get; }
        public string VaultPath => Path.Combine(Root, "vault.skvault");
        public string DirectoryPath(string name) => Path.Combine(Root, name);
        public void Dispose() { try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { } }
    }
}
