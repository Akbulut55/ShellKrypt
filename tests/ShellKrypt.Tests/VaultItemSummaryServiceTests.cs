using ShellKrypt.Application.Items;
using ShellKrypt.Application.Notes;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class VaultItemSummaryServiceTests
{
    private const string MasterPassword = "correct horse battery staple 2026!";

    [Fact]
    public async Task SummariesIncludeCountsWithoutExposingSecrets()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);
        var cardExpiry = DateTimeOffset.UtcNow.AddMonths(1);

        await fixture.WebLogins.AddAsync(workspace.VaultPath, fixture.VaultKey, new WebLoginInput(
            "GitHub",
            "https://github.com",
            "octo",
            "octo@example.com",
            "weak",
            ""));
        await fixture.WebLogins.AddAsync(workspace.VaultPath, fixture.VaultKey, new WebLoginInput(
            "Mail",
            "https://mail.example.com",
            "octo",
            "octo@example.com",
            "weak",
            ""));
        await fixture.Cards.AddAsync(workspace.VaultPath, fixture.VaultKey, new CardInput(
            "Personal Visa",
            "Main Bank",
            "Ada Lovelace",
            "4111111111111111",
            cardExpiry.Month,
            cardExpiry.Year,
            "123",
            "",
            "Visa",
            "Personal"));
        var result = await fixture.Summaries.ListAsync(
            workspace.VaultPath,
            fixture.VaultKey,
            ItemListQuery.Default(pageSize: 20));

        Assert.Equal(3, result.Counts.Total);
        Assert.Equal(2, result.Counts.WebLogins);
        Assert.Equal(1, result.Counts.Cards);
        Assert.Equal(2, result.Counts.WeakPasswords);
        Assert.Equal(2, result.Counts.ReusedPasswords);
        Assert.Equal(1, result.Counts.ExpiringSoonCards);
        Assert.DoesNotContain(result.AllItems, item => item.SearchText.Contains("4111111111111111", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QueryFiltersSortsAndPaginatesSummaries()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);

        await fixture.WebLogins.AddAsync(workspace.VaultPath, fixture.VaultKey, new WebLoginInput(
            "Beta",
            "https://beta.example.com",
            "beta",
            "",
            "StrongPassword123!",
            ""));
        await fixture.WebLogins.AddAsync(workspace.VaultPath, fixture.VaultKey, new WebLoginInput(
            "Alpha",
            "https://alpha.example.com",
            "alpha",
            "",
            "AnotherStrongPassword123!",
            ""));
        await fixture.Notes.AddAsync(workspace.VaultPath, fixture.VaultKey, new NoteInput(
            "Alpha note",
            "note content",
            false));

        var result = await fixture.Summaries.ListAsync(
            workspace.VaultPath,
            fixture.VaultKey,
            new ItemListQuery(
                SearchText: "example.com",
                TypeFilter: ItemListFilters.Web,
                ScopeFilter: ItemListFilters.All,
                SortMode: ItemListSortModes.Alphabetical,
                Page: 1,
                PageSize: 1));

        Assert.Equal(2, result.Page.TotalCount);
        var item = Assert.Single(result.Page.Items);
        Assert.Equal("Alpha", item.Title);
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
            new WebLoginService(itemRepository),
            new CardService(itemRepository),
            new NoteService(new EncryptedNoteStore(itemRepository)),
            new VaultItemSummaryService(itemRepository, new VaultItemPayloadReader()));
    }

    private sealed record Fixture(
        byte[] VaultKey,
        WebLoginService WebLogins,
        CardService Cards,
        NoteService Notes,
        VaultItemSummaryService Summaries);

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKryptSummaryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            VaultPath = Path.Combine(Root, "test.skvault");
        }

        public string Root { get; }
        public string VaultPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
