using System.Text;
using ShellKrypt.Application.QuickFill;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class QuickFillEntryServiceTests
{
    private const string MasterPassword = "quick fill master password 2026!";
    private const string KnownOwnedSecret = "entry-owned-secret-value-2026";

    [Fact]
    public async Task Entries_CreateUpdateDeleteAndEncryptOwnedFields()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);

        var added = await fixture.QuickFill.AddAsync(workspace.VaultPath, fixture.VaultKey, BuildInput("Chrome GitHub", KnownOwnedSecret));

        Assert.Equal("Chrome GitHub", added.Name);
        Assert.Equal("Developer Tools", added.Category);
        Assert.Equal("chrome", added.Target.ProcessName);
        Assert.Equal(KnownOwnedSecret, added.Fields.Single(field => field.Id == "password").Value);

        var listed = Assert.Single(await fixture.QuickFill.ListAsync(workspace.VaultPath, fixture.VaultKey));
        Assert.Equal(added.Id, listed.Id);

        var rawText = Encoding.UTF8.GetString(ReadWorkspaceBytes(workspace.Root));
        Assert.DoesNotContain(KnownOwnedSecret, rawText, StringComparison.Ordinal);

        var updated = await fixture.QuickFill.UpdateAsync(
            workspace.VaultPath,
            fixture.VaultKey,
            added.Id,
            added.CreatedAtUtc,
            BuildInput("Chrome Work", "updated-secret"));

        Assert.Equal("Chrome Work", updated.Name);

        await fixture.QuickFill.DeleteAsync(workspace.VaultPath, added.Id);
        Assert.Empty(await fixture.QuickFill.ListAsync(workspace.VaultPath, fixture.VaultKey));
    }

    [Fact]
    public void MatchingAndPreview_UseSafeTargetAndMaskedSelectedFields()
    {
        var entry = new QuickFillEntry(
            Id: "entry-1",
            Name: "Chrome GitHub",
            Category: "Developer Tools",
            Enabled: true,
            Target: new QuickFillTargetRule("chrome", "GitHub"),
            Fields:
            [
                new QuickFillField("username", "Username", QuickFillFieldKind.Username, false, 0, QuickFillFieldSourceKind.Owned, "octo", "", "", ""),
                new QuickFillField("email", "Email", QuickFillFieldKind.Text, false, 1, QuickFillFieldSourceKind.Owned, "octo@example.com", "", "", ""),
                new QuickFillField("password", "Password", QuickFillFieldKind.Password, true, 2, QuickFillFieldSourceKind.Owned, KnownOwnedSecret, "", "", "")
            ],
            PressEnterAfterFill: false,
            Notes: "",
            CreatedAtUtc: "2026-06-16T00:00:00.0000000+00:00",
            UpdatedAtUtc: "2026-06-16T00:00:00.0000000+00:00");

        Assert.True(QuickFillMatcher.IsMatch(entry, new QuickFillTargetContext("chrome.exe", "GitHub - Chromium")));
        Assert.False(QuickFillMatcher.IsMatch(entry, new QuickFillTargetContext("notepad", "GitHub - Chromium")));
        Assert.False(QuickFillMatcher.IsMatch(entry with { Enabled = false }, new QuickFillTargetContext("chrome.exe", "GitHub - Chromium")));
        Assert.True(QuickFillMatcher.IsMatch(entry with { Target = new QuickFillTargetRule("chrome.exe", "") }, new QuickFillTargetContext("chrome", "Any title")));
        Assert.False(QuickFillMatcher.IsMatch(entry, new QuickFillTargetContext("chrome.exe", "Other tab")));
        Assert.True(QuickFillMatcher.IsProcessMatch(entry, new QuickFillTargetContext("chrome.exe", "Other tab")));

        var preview = QuickFillSequencePreviewer.BuildPreview(entry);
        Assert.Equal(["Username", "Password (masked)"], preview);
        Assert.DoesNotContain(KnownOwnedSecret, string.Join(" ", preview), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LinkedWebLoginSelection_StoresOnlySelectedFieldsInQuickFillEntry()
    {
        using var workspace = new TempWorkspace();
        var fixture = await CreateUnlockedFixtureAsync(workspace.VaultPath);
        var login = await fixture.WebLogins.AddAsync(workspace.VaultPath, fixture.VaultKey, new WebLoginInput(
            "Game Portal",
            "https://game.example.com",
            "game-user",
            "game@example.com",
            "game-password",
            ""));

        var entry = await fixture.QuickFill.AddAsync(workspace.VaultPath, fixture.VaultKey, new QuickFillEntryInput(
            "Game Login",
            "Games",
            true,
            new QuickFillTargetRule("game", ""),
            [
                new QuickFillField("username", "Username", QuickFillFieldKind.Username, false, 0, QuickFillFieldSourceKind.WebLogin, "", login.Id, "", "username"),
                new QuickFillField("password", "Password", QuickFillFieldKind.Password, true, 1, QuickFillFieldSourceKind.WebLogin, "", login.Id, "", "password")
            ],
            false,
            ""));

        Assert.Equal(2, entry.Fields.Count);
        Assert.Contains(entry.Fields, field => field.LinkedFieldName == "username");
        Assert.Contains(entry.Fields, field => field.LinkedFieldName == "password");
        Assert.DoesNotContain(entry.Fields, field => field.LinkedFieldName == "email");
        Assert.DoesNotContain(entry.Fields, field => field.LinkedFieldName == "url");
    }

    private static QuickFillEntryInput BuildInput(string name, string secret) => new(
        Name: name,
        Category: "Developer Tools",
        Enabled: true,
        Target: new QuickFillTargetRule("chrome.exe", "GitHub"),
        Fields:
        [
            new QuickFillField("username", "Username", QuickFillFieldKind.Username, false, 0, QuickFillFieldSourceKind.Owned, "octo", "", "", ""),
            new QuickFillField("password", "Password", QuickFillFieldKind.Password, true, 1, QuickFillFieldSourceKind.Owned, secret, "", "", "")
        ],
        PressEnterAfterFill: false,
        Notes: "");

    private static async Task<Fixture> CreateUnlockedFixtureAsync(string vaultPath)
    {
        var vaultService = new SqliteVaultService();
        var itemRepository = new SqliteItemRepository();
        await vaultService.CreateAsync(vaultPath, MasterPassword);
        var unlock = await vaultService.UnlockAsync(vaultPath, MasterPassword);
        Assert.True(unlock.Success, unlock.Error);

        return new Fixture(unlock.VaultKey!, new QuickFillEntryService(itemRepository), new WebLoginService(itemRepository));
    }

    private static byte[] ReadWorkspaceBytes(string root)
    {
        using var stream = new MemoryStream();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        {
            var bytes = File.ReadAllBytes(file);
            stream.Write(bytes, 0, bytes.Length);
        }

        return stream.ToArray();
    }

    private sealed record Fixture(byte[] VaultKey, QuickFillEntryService QuickFill, WebLoginService WebLogins);

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKryptQuickFillTests", Guid.NewGuid().ToString("N"));
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
