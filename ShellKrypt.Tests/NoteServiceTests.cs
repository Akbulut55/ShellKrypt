using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class NoteServiceTests
{
    [Fact]
    public async Task AddUpdateDelete_RoundTripsNote()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new NoteService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "vault-pass");

        var added = await service.AddAsync(
            vaultPath,
            vaultKey,
            new NoteInput(
                Title: "  Deployment Runbook  ",
                Content: "# Heading",
                Favorite: true));

        Assert.Equal("Deployment Runbook", added.Title);
        Assert.Equal("# Heading", added.Content);
        Assert.True(added.Favorite);

        var listed = await service.ListAsync(vaultPath, vaultKey);
        Assert.Single(listed);
        Assert.Equal(added, listed[0]);

        var updated = await service.UpdateAsync(
            vaultPath,
            vaultKey,
            added.Id,
            added.CreatedAtUtc,
            new NoteInput(
                Title: "Deployment Runbook v2",
                Content: "Updated content",
                Favorite: false));

        Assert.Equal(added.Id, updated.Id);
        Assert.Equal(added.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Deployment Runbook v2", updated.Title);
        Assert.Equal("Updated content", updated.Content);
        Assert.False(updated.Favorite);

        listed = await service.ListAsync(vaultPath, vaultKey);
        Assert.Single(listed);
        Assert.Equal(updated, listed[0]);

        await service.DeleteAsync(vaultPath, added.Id);

        Assert.Empty(await service.ListAsync(vaultPath, vaultKey));
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
