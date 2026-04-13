using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class WebLoginServiceTests
{
    [Fact]
    public async Task AddUpdateDelete_RoundTripsWebLogin()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new WebLoginService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "vault-pass");

        var added = await service.AddAsync(
            vaultPath,
            vaultKey,
            new WebLoginInput(
                Title: " ShellKrypt ",
                Url: " https://example.com ",
                Username: " shellkrypt-user ",
                Email: " user@example.com ",
                Password: "secret",
                Notes: " private notes "));

        Assert.Equal("ShellKrypt", added.Title);
        Assert.Equal("https://example.com", added.Url);
        Assert.Equal("shellkrypt-user", added.Username);
        Assert.Equal("user@example.com", added.Email);
        Assert.Equal("secret", added.Password);
        Assert.Equal("private notes", added.Notes);

        var listed = await service.ListAsync(vaultPath, vaultKey);
        Assert.Single(listed);
        Assert.Equal(added, listed[0]);

        var updated = await service.UpdateAsync(
            vaultPath,
            vaultKey,
            added.Id,
            added.CreatedAtUtc,
            new WebLoginInput(
                Title: "ShellKrypt Admin",
                Url: "https://admin.example.com",
                Username: "admin",
                Email: "admin@example.com",
                Password: "new-secret",
                Notes: "rotated"));

        Assert.Equal(added.Id, updated.Id);
        Assert.Equal(added.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("ShellKrypt Admin", updated.Title);
        Assert.Equal("new-secret", updated.Password);

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
