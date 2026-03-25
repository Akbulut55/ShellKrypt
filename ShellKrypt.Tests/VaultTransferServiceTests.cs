using System.Text;
using System.Text.Json;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class VaultTransferServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task EncryptedExport_RoundTripsItemsAndLabels()
    {
        using var workspace = new TempWorkspace();
        var repo = new SqliteItemRepository();
        var transfer = new SqliteVaultTransferService();
        var vaultService = new SqliteVaultService();

        var sourceVault = workspace.FilePath("source.skvault");
        var targetVault = workspace.FilePath("target.skvault");
        var exportPath = workspace.FilePath("backup.skbx");

        var sourceKey = await CreateAndUnlockVaultAsync(vaultService, sourceVault, "source-pass");
        var targetKey = await CreateAndUnlockVaultAsync(vaultService, targetVault, "target-pass");

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");
        var webId = Guid.NewGuid().ToString("N");
        var noteId = Guid.NewGuid().ToString("N");

        var label = await repo.UpsertLabelAsync(sourceVault, "Work", "#0f0f0f");
        await InsertWebAsync(repo, sourceVault, sourceKey, webId, "GitHub", "https://github.com", "octocat", "secret123", "repo access", "use app password", "JBSWY3DPEHPK3PXP", createdAt, updatedAt, favorite: true);
        await InsertNoteAsync(repo, sourceVault, sourceKey, noteId, "Travel", "Pack passport and charger", createdAt, updatedAt);
        await repo.SetItemLabelsAsync(sourceVault, webId, new[] { label.Id });

        await transfer.ExportEncryptedAsync(sourceVault, sourceKey, exportPath, "backup-pass");

        var summary = await transfer.GetEncryptedImportSummaryAsync(exportPath, "backup-pass");
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(1, summary.WebCount);
        Assert.Equal(0, summary.CardCount);
        Assert.Equal(1, summary.NoteCount);
        Assert.Equal(1, summary.LabelCount);
        Assert.Equal(1, summary.FavoriteCount);

        await transfer.ImportEncryptedAsync(exportPath, "backup-pass", targetVault, targetKey);

        var targetRows = await repo.ListAsync(targetVault);
        Assert.Equal(2, targetRows.Count);

        var importedWeb = targetRows.Single(x => x.Header.Id == webId);
        Assert.True(importedWeb.Header.Favorite);
        Assert.Single(importedWeb.Labels);
        Assert.Equal("Work", importedWeb.Labels[0].Name);

        var webPayload = JsonSerializer.Deserialize<WebPayload>(
            Encoding.UTF8.GetString(AesGcmBlob.Decrypt(targetKey, importedWeb.EncryptedPayload)),
            JsonOptions);

        Assert.NotNull(webPayload);
        Assert.Equal("GitHub", webPayload!.Title);
        Assert.Equal("https://github.com", webPayload.Url);
        Assert.Equal("octocat", webPayload.Username);
        Assert.Equal("secret123", webPayload.Password);
        Assert.Equal("repo access", webPayload.Notes);
        Assert.Equal("use app password", webPayload.TwoFaNote);
        Assert.Equal("JBSWY3DPEHPK3PXP", webPayload.TotpSecret);
    }

    [Fact]
    public async Task PlaintextExport_WritesReadableSnapshotJson()
    {
        using var workspace = new TempWorkspace();
        var repo = new SqliteItemRepository();
        var transfer = new SqliteVaultTransferService();
        var vaultService = new SqliteVaultService();

        var vaultPath = workspace.FilePath("vault.skvault");
        var exportPath = workspace.FilePath("export.json");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "vault-pass");

        var itemId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");

        await InsertWebAsync(repo, vaultPath, vaultKey, itemId, "GitLab", "https://gitlab.com", "codex", "password!", "notes", "", "", createdAt, updatedAt, favorite: false);

        await transfer.ExportPlaintextJsonAsync(vaultPath, vaultKey, exportPath);

        var json = await File.ReadAllTextAsync(exportPath);
        var snapshot = JsonSerializer.Deserialize<VaultSnapshot>(json, JsonOptions);

        Assert.NotNull(snapshot);
        Assert.Single(snapshot!.Items);
        Assert.Empty(snapshot.Labels);
        Assert.Equal("GitLab", JsonSerializer.Deserialize<WebPayload>(snapshot.Items[0].PayloadJson, JsonOptions)!.Title);
    }

    [Fact]
    public async Task CsvPreview_ReportsDuplicatesAndInvalidRows()
    {
        using var workspace = new TempWorkspace();
        var repo = new SqliteItemRepository();
        var transfer = new SqliteVaultTransferService();
        var vaultService = new SqliteVaultService();

        var vaultPath = workspace.FilePath("vault.skvault");
        var csvPath = workspace.FilePath("import.csv");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "vault-pass");

        var existingId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-15).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");
        await InsertWebAsync(repo, vaultPath, vaultKey, existingId, "GitHub", "https://github.com", "octocat", "oldpass", "", "", "", createdAt, updatedAt, favorite: false);

        await File.WriteAllTextAsync(csvPath, """
Type,Title,Url,Username,Password,Notes,Content,Cardholder,Number,ExpiryMonth,ExpiryYear,Cvc
Web,GitHub,https://github.com,octocat,newpass,Imported duplicate,,,,,,
Note,Travel,,,,,,Pack passport,,,,,
Card,Card,,,,,,Jane,12345,12,2030,123
""");

        var preview = await transfer.PreviewCsvImportAsync(vaultPath, vaultKey, csvPath);

        Assert.Equal(3, preview.TotalRows);
        Assert.Equal(1, preview.NewRows);
        Assert.Equal(1, preview.DuplicateRows);
        Assert.Equal(1, preview.InvalidRows);
        Assert.Contains(preview.Rows, x => x.Status == VaultCsvRowStatus.Duplicate && x.Title == "GitHub");
        Assert.Contains(preview.Rows, x => x.Status == VaultCsvRowStatus.New && x.Title == "Travel");
        Assert.Contains(preview.Rows, x => x.Status == VaultCsvRowStatus.Invalid && x.Title == "Card");
    }

    [Fact]
    public async Task CsvImport_OverwriteDuplicates_ReplacesExistingItem()
    {
        using var workspace = new TempWorkspace();
        var repo = new SqliteItemRepository();
        var transfer = new SqliteVaultTransferService();
        var vaultService = new SqliteVaultService();

        var vaultPath = workspace.FilePath("vault.skvault");
        var csvPath = workspace.FilePath("overwrite.csv");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "vault-pass");

        var itemId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-15).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");
        await InsertWebAsync(repo, vaultPath, vaultKey, itemId, "GitHub", "https://github.com", "octocat", "oldpass", "", "", "", createdAt, updatedAt, favorite: false);

        await File.WriteAllTextAsync(csvPath, """
Type,Title,Url,Username,Password,Notes
Web,GitHub,https://github.com,octocat,newpass,Imported overwrite
""");

        await transfer.ImportCsvAsync(vaultPath, vaultKey, csvPath, VaultCsvDuplicateStrategy.OverwriteDuplicates);

        var rows = await repo.ListAsync(vaultPath);
        Assert.Single(rows);

        var payload = JsonSerializer.Deserialize<WebPayload>(
            Encoding.UTF8.GetString(AesGcmBlob.Decrypt(vaultKey, rows[0].EncryptedPayload)),
            JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal("newpass", payload!.Password);
        Assert.Equal("Imported overwrite", payload.Notes);
    }

    private static async Task<byte[]> CreateAndUnlockVaultAsync(SqliteVaultService vaultService, string vaultPath, string masterPassword)
    {
        await vaultService.CreateAsync(vaultPath, masterPassword);
        var result = await vaultService.UnlockAsync(vaultPath, masterPassword);

        if (!result.Success || result.VaultKey is null)
            throw new InvalidOperationException(result.Error ?? "Unable to unlock vault.");

        return result.VaultKey;
    }

    private static async Task InsertWebAsync(
        SqliteItemRepository repo,
        string vaultPath,
        byte[] vaultKey,
        string id,
        string title,
        string url,
        string username,
        string password,
        string notes,
        string twoFaNote,
        string totpSecret,
        string createdAtUtc,
        string updatedAtUtc,
        bool favorite)
    {
        var payload = new WebPayload(title, url, username, password, notes, twoFaNote, totpSecret);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var encrypted = AesGcmBlob.Encrypt(vaultKey, json);
        var header = new VaultItemHeader(id, ItemType.Web, favorite, createdAtUtc, updatedAtUtc);

        await repo.InsertAsync(vaultPath, header, encrypted);
    }

    private static async Task InsertNoteAsync(
        SqliteItemRepository repo,
        string vaultPath,
        byte[] vaultKey,
        string id,
        string title,
        string content,
        string createdAtUtc,
        string updatedAtUtc)
    {
        var payload = new NotePayload(title, content);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var encrypted = AesGcmBlob.Encrypt(vaultKey, json);
        var header = new VaultItemHeader(id, ItemType.Note, false, createdAtUtc, updatedAtUtc);

        await repo.InsertAsync(vaultPath, header, encrypted);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string FilePath(string fileName) => System.IO.Path.Combine(Root, fileName);

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
