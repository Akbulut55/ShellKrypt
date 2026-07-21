using ShellKrypt.Application.Notes;
using Microsoft.Data.Sqlite;
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
        var service = new NoteService(new EncryptedNoteStore(repo));
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var addResult = await service.AddAsync(
            vaultPath,
            vaultKey,
            new NoteInput(
                Title: "  Deployment Runbook  ",
                Content: "# Heading",
                Favorite: true));

        Assert.True(addResult.Success);
        var added = Assert.IsType<NoteEntry>(addResult.Entry);
        Assert.Equal("Deployment Runbook", added.Title);
        Assert.Equal("# Heading", added.Content);
        Assert.True(added.Favorite);

        var listed = await service.LoadAsync(vaultPath, vaultKey);
        Assert.True(listed.Success);
        Assert.Single(listed.Entries);
        Assert.Equal(added, listed.Entries[0]);

        var updateResult = await service.UpdateAsync(
            vaultPath,
            vaultKey,
            added.Id,
            added.CreatedAtUtc,
            new NoteInput(
                Title: "Deployment Runbook v2",
                Content: "Updated content",
                Favorite: false));

        Assert.True(updateResult.Success);
        var updated = Assert.IsType<NoteEntry>(updateResult.Entry);
        Assert.Equal(added.Id, updated.Id);
        Assert.Equal(added.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Deployment Runbook v2", updated.Title);
        Assert.Equal("Updated content", updated.Content);
        Assert.False(updated.Favorite);

        listed = await service.LoadAsync(vaultPath, vaultKey);
        Assert.Single(listed.Entries);
        Assert.Equal(updated, listed.Entries[0]);

        var deleteResult = await service.DeleteAsync(vaultPath, added.Id);
        Assert.True(deleteResult.Success);

        Assert.Empty((await service.LoadAsync(vaultPath, vaultKey)).Entries);
    }

    [Fact]
    public async Task Load_SkipsIndividuallyCorruptEncryptedRows()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new NoteService(new EncryptedNoteStore(repo));
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");
        var first = await service.AddAsync(vaultPath, vaultKey, new NoteInput("First", "one", false));
        var second = await service.AddAsync(vaultPath, vaultKey, new NoteInput("Second", "two", false));
        Assert.True(first.Success && second.Success);

        await using (var connection = new SqliteConnection($"Data Source={vaultPath}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "UPDATE items SET encryptedPayload = X'010203' WHERE id = $id";
            command.Parameters.AddWithValue("$id", first.Entry!.Id);
            await command.ExecuteNonQueryAsync();
        }

        var result = await service.LoadAsync(vaultPath, vaultKey);

        Assert.True(result.Success);
        Assert.Equal(1, result.SkippedCorruptEntries);
        Assert.Equal("Second", Assert.Single(result.Entries).Title);
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
