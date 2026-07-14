using ShellKrypt.Core.Authenticator;
using ShellKrypt.Infrastructure.Authenticator;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests.Authenticator;

public sealed class AuthenticatorEntryServiceTests
{
    [Fact]
    public async Task AddUpdateMarkUsedDelete_RoundTripsAuthenticator()
    {
        using var workspace = new AuthenticatorTestWorkspace();
        var vaultService = new SqliteVaultService();
        var service = new AuthenticatorEntryService(new SqliteItemRepository());
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await AuthenticatorTestWorkspace.CreateAndUnlockVaultAsync(vaultService, vaultPath);

        var added = await service.AddAsync(
            vaultPath,
            vaultKey,
            new AuthenticatorInput(" GitHub ", "JBSWY3DPEHPK3PXP", AuthenticatorKeyType.TimeBased));

        Assert.Equal("GitHub", added.Name);
        Assert.Equal("JBSWY3DPEHPK3PXP", added.Secret);
        Assert.Equal(AuthenticatorKeyType.TimeBased, added.KeyType);
        Assert.Equal("HMAC-SHA1", added.Algorithm);
        Assert.Equal(6, added.Digits);
        Assert.Equal(30, added.PeriodSeconds);
        Assert.Equal(string.Empty, added.LastUsedAtUtc);
        Assert.Equal(added, Assert.Single(await service.ListAsync(vaultPath, vaultKey)));

        var updated = await service.UpdateAsync(
            vaultPath,
            vaultKey,
            added.Id,
            added.CreatedAtUtc,
            new AuthenticatorInput(
                "Build Agent",
                "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
                AuthenticatorKeyType.CounterBased,
                12,
                "HMAC-SHA256",
                8,
                45));

        Assert.Equal(added.Id, updated.Id);
        Assert.Equal(added.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal(12, updated.Counter);
        Assert.Equal("HMAC-SHA256", updated.Algorithm);
        Assert.Equal(8, updated.Digits);

        var marked = await service.MarkUsedAsync(vaultPath, vaultKey, updated.Id);
        Assert.Equal(13, marked.Counter);
        Assert.False(string.IsNullOrWhiteSpace(marked.LastUsedAtUtc));

        await service.DeleteAsync(vaultPath, updated.Id);
        Assert.Empty(await service.ListAsync(vaultPath, vaultKey));
    }
}
