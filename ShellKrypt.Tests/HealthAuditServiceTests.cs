using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class HealthAuditServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_FindsReusedWeakAndOldLogins()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);
        var audit = new HealthAuditService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var first = await webLogins.AddAsync(
            vaultPath,
            vaultKey,
            new WebLoginInput(
                Title: "Admin",
                Url: "https://admin.example.com",
                Username: "admin",
                Email: "admin@example.com",
                Password: "weak",
                Notes: ""));

        var second = await webLogins.AddAsync(
            vaultPath,
            vaultKey,
            new WebLoginInput(
                Title: "Legacy",
                Url: "https://legacy.example.com",
                Username: "legacy",
                Email: "legacy@example.com",
                Password: "weak",
                Notes: ""));

        var service = new NoteService(repo);
        await service.AddAsync(vaultPath, vaultKey, new NoteInput("Note", "Content", false));

        await repo.UpdateAsync(
            vaultPath,
            new VaultItemHeader(first.Id, ItemType.Web, false, first.CreatedAtUtc, DateTimeOffset.UtcNow.AddDays(-120).ToString("O")),
            (await repo.ListAsync(vaultPath, vaultKey)).First(x => x.Header.Id == first.Id).EncryptedPayload);

        var result = await audit.AnalyzeAsync(vaultPath, vaultKey);

        Assert.Equal(2, result.AnalyzedCount);
        Assert.Equal(2, result.ReusedCount);
        Assert.Equal(2, result.WeakCount);
        Assert.Equal(1, result.OldCount);
        Assert.Contains(result.Issues, issue => issue.Category == "Reused");
        Assert.Contains(result.Issues, issue => issue.Category == "Weak");
        Assert.Contains(result.Issues, issue => issue.Category == "Old");
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
