using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class CardServiceTests
{
    [Fact]
    public async Task AddUpdateDelete_RoundTripsCard()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new CardService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "vault-pass");

        var added = await service.AddAsync(
            vaultPath,
            vaultKey,
            new CardInput(
                Title: " Titanium Business ",
                Bank: " Chase ",
                Cardholder: " Alex Morgan ",
                Number: "4242 4242 4242 4242 9999",
                ExpiryMonth: 9,
                ExpiryYear: 2028,
                Cvc: "12345",
                Notes: " rewards ",
                Issuer: " Visa ",
                CardType: " Credit Card "));

        Assert.Equal("Titanium Business", added.Title);
        Assert.Equal("Chase", added.Bank);
        Assert.Equal("Alex Morgan", added.Cardholder);
        Assert.Equal("4242424242424242", added.Number);
        Assert.Equal(9, added.ExpiryMonth);
        Assert.Equal(2028, added.ExpiryYear);
        Assert.Equal("1234", added.Cvc);
        Assert.Equal("rewards", added.Notes);
        Assert.Equal("Visa", added.Issuer);
        Assert.Equal("Credit Card", added.CardType);

        var listed = await service.ListAsync(vaultPath, vaultKey);
        Assert.Single(listed);
        Assert.Equal(added, listed[0]);

        var updated = await service.UpdateAsync(
            vaultPath,
            vaultKey,
            added.Id,
            added.CreatedAtUtc,
            new CardInput(
                Title: "Travel Debit",
                Bank: "Local Bank",
                Cardholder: "Alex Morgan",
                Number: "5555 5555 5555 4444",
                ExpiryMonth: 12,
                ExpiryYear: 2030,
                Cvc: "987",
                Notes: "travel only",
                Issuer: "Mastercard",
                CardType: "Debit Card"));

        Assert.Equal(added.Id, updated.Id);
        Assert.Equal(added.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Travel Debit", updated.Title);
        Assert.Equal("5555555555554444", updated.Number);
        Assert.Equal("Debit Card", updated.CardType);

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
