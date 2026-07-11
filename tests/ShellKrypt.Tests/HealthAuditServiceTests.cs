using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class HealthAuditServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_FindsWebLoginCardApiKeyAndSettingsFindings()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);
        var cards = new CardService(repo);
        var apiKeys = new ApiKeyService(repo);
        var audit = new HealthAuditService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var reusedA = await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Admin", "https://admin.example.com", "admin", "", "weak", ""));
        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Legacy", "https://legacy.example.com", "legacy", "", "weak", ""));
        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Empty", "https://empty.example.com", "empty", "", "", ""));
        await MarkUpdatedAsync(repo, vaultPath, vaultKey, reusedA.Id, ItemType.Web, daysAgo: 120);

        await cards.AddAsync(vaultPath, vaultKey, new CardInput("Expired Card", "Bank", "Tester", "4111111111111111", 1, 2024, "123", "", "Visa", "Credit Card"));
        var soon = DateTimeOffset.UtcNow.AddMonths(1);
        await cards.AddAsync(vaultPath, vaultKey, new CardInput("Expiring Card", "Bank", "Tester", "5555555555554444", soon.Month, soon.Year, "456", "", "Mastercard", "Credit Card"));

        var oldApi = await apiKeys.AddAsync(vaultPath, vaultKey, new ApiKeyInput(
            "Stripe production",
            "Stripe",
            "Production",
            "",
            [
                new ApiKeyFieldInput("field-1", "API Key", "API Key", "shared-api-secret", true, true, 0)
            ]));
        await apiKeys.AddAsync(vaultPath, vaultKey, new ApiKeyInput(
            "Stripe staging",
            "Stripe",
            "Staging",
            "",
            [
                new ApiKeyFieldInput("field-1", "API Key", "API Key", "shared-api-secret", true, true, 0)
            ]));
        await apiKeys.AddAsync(vaultPath, vaultKey, new ApiKeyInput(
            "Metadata only",
            "Internal",
            "Production",
            "",
            [
                new ApiKeyFieldInput("field-1", "Project ID", "Project ID", "project-123", false, false, 0)
            ]));
        await MarkUpdatedAsync(repo, vaultPath, vaultKey, oldApi.Id, ItemType.ApiKey, daysAgo: 220);

        var result = await audit.AnalyzeAsync(
            vaultPath,
            vaultKey,
            new HealthAuditOptions(
                AutoLockEnabled: false,
                LockOnDeactivate: false,
                ClipboardClearSeconds: 120,
                ClipboardCopyEnabled: true));

        Assert.Equal(8, result.AnalyzedCount);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.ReusedPassword);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.WeakPassword);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.EmptyPassword);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.StaleCredential);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.ExpiredCard);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.ExpiringCard);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.ReusedApiSecret);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.OldApiKey);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.ApiKeyMissingSecret);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.AutoLockDisabled);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.FocusLockDisabled);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.ClipboardTimeoutLong);
        Assert.Contains(result.Issues, issue => issue.Category == HealthAuditCategory.ClipboardCopyEnabled);
        Assert.True(result.HighRiskCount > 0);
        Assert.True(result.PasswordIssueCount > 0);
        Assert.True(result.CardIssueCount > 0);
        Assert.True(result.ApiKeyIssueCount > 0);
        Assert.Equal(4, result.SettingsIssueCount);
    }

    [Fact]
    public async Task AnalyzeAsync_DoesNotExposeSecretValuesInFindings()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);
        var cards = new CardService(repo);
        var apiKeys = new ApiKeyService(repo);
        var audit = new HealthAuditService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");
        const string password = "KnownPasswordWithoutDigit!";
        const string cardNumber = "4111111111111111";
        const string cvc = "123";
        const string apiSecret = "KnownApiSecret-Do-Not-Leak";

        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Login A", "https://a.example.com", "user-a", "", password, ""));
        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Login B", "https://b.example.com", "user-b", "", password, ""));
        await cards.AddAsync(vaultPath, vaultKey, new CardInput("Expired Card", "Bank", "Tester", cardNumber, 1, 2024, cvc, "", "Visa", "Credit Card"));
        await apiKeys.AddAsync(vaultPath, vaultKey, new ApiKeyInput(
            "API A",
            "Provider",
            "Production",
            "",
            [new ApiKeyFieldInput("field-1", "API Key", "API Key", apiSecret, true, true, 0)]));
        await apiKeys.AddAsync(vaultPath, vaultKey, new ApiKeyInput(
            "API B",
            "Provider",
            "Production",
            "",
            [new ApiKeyFieldInput("field-1", "API Key", "API Key", apiSecret, true, true, 0)]));

        var result = await audit.AnalyzeAsync(vaultPath, vaultKey, new HealthAuditOptions(ClipboardCopyEnabled: false));
        var visibleText = string.Join(
            "\n",
            result.Issues.Select(issue => $"{issue.Title} {issue.Details} {issue.AffectedItem}"));

        Assert.DoesNotContain(password, visibleText);
        Assert.DoesNotContain(cardNumber, visibleText);
        Assert.DoesNotContain(cvc, visibleText);
        Assert.DoesNotContain(apiSecret, visibleText);
        Assert.All(result.Issues, issue => Assert.False(string.IsNullOrWhiteSpace(issue.Fingerprint)));
        Assert.Equal(result.Issues.Count, result.Issues.Select(issue => issue.Fingerprint).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNoFindingsForHealthySyntheticData()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);
        var cards = new CardService(repo);
        var apiKeys = new ApiKeyService(repo);
        var audit = new HealthAuditService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");
        var future = DateTimeOffset.UtcNow.AddYears(2);

        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Healthy Login", "https://healthy.example.com", "healthy", "", "L0ng!Unique!Password!2026", ""));
        await cards.AddAsync(vaultPath, vaultKey, new CardInput("Healthy Card", "Bank", "Tester", "4111111111111111", future.Month, future.Year, "123", "", "Visa", "Credit Card"));
        await apiKeys.AddAsync(vaultPath, vaultKey, new ApiKeyInput(
            "Healthy API",
            "Provider",
            "Production",
            "",
            [new ApiKeyFieldInput("field-1", "API Key", "API Key", "unique-api-secret-2026", true, true, 0)]));

        var result = await audit.AnalyzeAsync(
            vaultPath,
            vaultKey,
            new HealthAuditOptions(
                AutoLockEnabled: true,
                LockOnDeactivate: true,
                ClipboardClearSeconds: 15,
                ClipboardCopyEnabled: false));

        Assert.Equal(3, result.AnalyzedCount);
        Assert.Empty(result.Issues);
        Assert.Equal(0, result.HighRiskCount);
        Assert.Equal(0, result.PasswordIssueCount);
        Assert.Equal(0, result.CardIssueCount);
        Assert.Equal(0, result.ApiKeyIssueCount);
        Assert.Equal(0, result.SettingsIssueCount);
    }

    private static async Task MarkUpdatedAsync(
        IItemRepository repo,
        string vaultPath,
        byte[] vaultKey,
        string itemId,
        ItemType itemType,
        int daysAgo)
    {
        var row = (await repo.ListAsync(vaultPath, vaultKey)).Single(item => item.Header.Id == itemId);
        var newHeader = new VaultItemHeader(
            row.Header.Id,
            itemType,
            row.Header.Favorite,
            row.Header.CreatedAtUtc,
            DateTimeOffset.UtcNow.AddDays(-daysAgo).ToString("O"));
        var plaintext = VaultPayloadProtector.DecryptItemPayload(vaultKey, row.Header, row.EncryptedPayload);
        var encryptedPayload = VaultPayloadProtector.EncryptItemPayload(vaultKey, newHeader, plaintext);

        await repo.UpdateAsync(
            vaultPath,
            newHeader,
            encryptedPayload);
    }

    private static async Task<byte[]> CreateAndUnlockVaultAsync(SqliteVaultService vaultService, string vaultPath, string masterPassword)
    {
        await vaultService.CreateAsync(vaultPath, masterPassword);
        var result = await vaultService.UnlockAsync(vaultPath, masterPassword);

        if (!result.Success || result.VaultKey is null)
            throw new InvalidOperationException(result.Error ?? "Unable to unlock vault.");

        return result.VaultKey;
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string FilePath(string fileName) => Path.Combine(Root, fileName);

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
