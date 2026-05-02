using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class AuthenticatorServiceTests
{
    [Fact]
    public async Task AddUpdateMarkUsedDelete_RoundTripsAuthenticator()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new AuthenticatorService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var added = await service.AddAsync(
            vaultPath,
            vaultKey,
            new AuthenticatorInput(
                Name: " GitHub ",
                Secret: "JBSWY3DPEHPK3PXP",
                KeyType: AuthenticatorKeyType.TimeBased));

        Assert.Equal("GitHub", added.Name);
        Assert.Equal("JBSWY3DPEHPK3PXP", added.Secret);
        Assert.Equal(AuthenticatorKeyType.TimeBased, added.KeyType);
        Assert.Equal(0, added.Counter);
        Assert.Equal("HMAC-SHA1", added.Algorithm);
        Assert.Equal(6, added.Digits);
        Assert.Equal(30, added.PeriodSeconds);
        Assert.Equal(string.Empty, added.LastUsedAtUtc);

        var listed = await service.ListAsync(vaultPath, vaultKey);
        Assert.Single(listed);
        Assert.Equal(added, listed[0]);

        var updated = await service.UpdateAsync(
            vaultPath,
            vaultKey,
            added.Id,
            added.CreatedAtUtc,
            new AuthenticatorInput(
                Name: "Build Agent",
                Secret: "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
                KeyType: AuthenticatorKeyType.CounterBased,
                Counter: 12,
                Algorithm: "HMAC-SHA256",
                Digits: 8,
                PeriodSeconds: 45));

        Assert.Equal(added.Id, updated.Id);
        Assert.Equal(added.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Build Agent", updated.Name);
        Assert.Equal(AuthenticatorKeyType.CounterBased, updated.KeyType);
        Assert.Equal(12, updated.Counter);
        Assert.Equal("HMAC-SHA256", updated.Algorithm);
        Assert.Equal(8, updated.Digits);
        Assert.Equal(45, updated.PeriodSeconds);

        var marked = await service.MarkUsedAsync(vaultPath, vaultKey, updated.Id);
        Assert.Equal(updated.Id, marked.Id);
        Assert.Equal(13, marked.Counter);
        Assert.False(string.IsNullOrWhiteSpace(marked.LastUsedAtUtc));
        Assert.NotEqual(updated.UpdatedAtUtc, marked.UpdatedAtUtc);

        await service.DeleteAsync(vaultPath, updated.Id);

        Assert.Empty(await service.ListAsync(vaultPath, vaultKey));
    }

    [Fact]
    public void GetCurrentCode_UsesDeterministicTotpVector()
    {
        var service = new AuthenticatorService(new SqliteItemRepository());
        var entry = new AuthenticatorEntry(
            Id: "auth-1",
            Name: "RFC6238",
            Secret: "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
            KeyType: AuthenticatorKeyType.TimeBased,
            Counter: 0,
            Algorithm: "HMAC-SHA1",
            Digits: 8,
            PeriodSeconds: 30,
            LastUsedAtUtc: "",
            CreatedAtUtc: DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));

        var snapshot = service.GetCurrentCode(entry, DateTimeOffset.FromUnixTimeSeconds(59));

        Assert.True(snapshot.IsValid);
        Assert.Equal("94287082", snapshot.Code);
        Assert.Equal(1, snapshot.SecondsRemaining);
    }

    [Fact]
    public void GetCurrentCode_UsesDeterministicHotpVector()
    {
        var service = new AuthenticatorService(new SqliteItemRepository());
        var entry = new AuthenticatorEntry(
            Id: "auth-2",
            Name: "RFC4226",
            Secret: "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
            KeyType: AuthenticatorKeyType.CounterBased,
            Counter: 0,
            Algorithm: "HMAC-SHA1",
            Digits: 6,
            PeriodSeconds: 30,
            LastUsedAtUtc: "",
            CreatedAtUtc: DateTimeOffset.UtcNow.ToString("O"),
            UpdatedAtUtc: DateTimeOffset.UtcNow.ToString("O"));

        var snapshot = service.GetCurrentCode(entry);

        Assert.True(snapshot.IsValid);
        Assert.Equal("755224", snapshot.Code);
        Assert.Equal(0, snapshot.SecondsRemaining);
    }

    [Theory]
    [InlineData("otpauth://totp/GitHub:octocat@example.com?secret=JBSWY3DPEHPK3PXP&issuer=GitHub", "GitHub", "JBSWY3DPEHPK3PXP", AuthenticatorKeyType.TimeBased, 0, "HMAC-SHA1", 6, 30)]
    [InlineData("otpauth://hotp/Build%20Server?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&counter=7", "Build Server", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", AuthenticatorKeyType.CounterBased, 7, "HMAC-SHA1", 6, 30)]
    [InlineData("otpauth://totp/Example?secret=JBSWY3DPEHPK3PXP&algorithm=SHA512&digits=8&period=45", "Example", "JBSWY3DPEHPK3PXP", AuthenticatorKeyType.TimeBased, 0, "HMAC-SHA512", 8, 45)]
    public void ParseOtpAuthUri_ReadsExpectedFields(string uri, string expectedName, string expectedSecret, AuthenticatorKeyType expectedType, long expectedCounter, string expectedAlgorithm, int expectedDigits, int expectedPeriod)
    {
        var parsed = OtpAuthUriParser.Parse(uri);

        Assert.Equal(expectedName, parsed.Name);
        Assert.Equal(expectedSecret, parsed.Secret);
        Assert.Equal(expectedType, parsed.KeyType);
        Assert.Equal(expectedCounter, parsed.Counter);
        Assert.Equal(expectedAlgorithm, parsed.Algorithm);
        Assert.Equal(expectedDigits, parsed.Digits);
        Assert.Equal(expectedPeriod, parsed.PeriodSeconds);
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
