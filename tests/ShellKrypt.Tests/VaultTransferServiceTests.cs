using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ShellKrypt.Core.Backups;
using ShellKrypt.Core.DataTransfer;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Backups;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.DataTransfer;
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
        var backups = new EncryptedVaultBackupService();
        var vaultService = new SqliteVaultService();

        var sourceVault = workspace.FilePath("source.skvault");
        var targetVault = workspace.FilePath("target.skvault");
        var exportPath = workspace.FilePath("backup.skbx");

        var sourceKey = await CreateAndUnlockVaultAsync(vaultService, sourceVault, "Source Vault Passphrase 2026");
        var targetKey = await CreateAndUnlockVaultAsync(vaultService, targetVault, "Target Vault Passphrase 2026");

        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");
        var webId = Guid.NewGuid().ToString("N");
        var noteId = Guid.NewGuid().ToString("N");

        var label = await repo.UpsertLabelAsync(sourceVault, sourceKey, "Work", "#0f0f0f");
        await InsertWebAsync(repo, sourceVault, sourceKey, webId, "GitHub", "https://github.com", "octocat", "secret123", "repo access", createdAt, updatedAt, favorite: true);
        await InsertNoteAsync(repo, sourceVault, sourceKey, noteId, "Travel", "Pack passport and charger", createdAt, updatedAt);
        await repo.SetItemLabelsAsync(sourceVault, webId, new[] { label.Id });

        await backups.CreateAsync(sourceVault, sourceKey, exportPath, "backup-pass");

        var summary = await backups.InspectAsync(exportPath, "backup-pass");
        Assert.Equal(2, summary.ItemCount);
        Assert.Equal(1, summary.WebCount);
        Assert.Equal(0, summary.CardCount);
        Assert.Equal(1, summary.NoteCount);
        Assert.Equal(0, summary.AuthenticatorCount);
        Assert.Equal(0, summary.ApiKeyCount);
        Assert.Equal(1, summary.LabelCount);
        Assert.Equal(1, summary.FavoriteCount);

        await backups.RestoreAsync(exportPath, "backup-pass", targetVault, targetKey);

        var targetRows = await repo.ListAsync(targetVault, targetKey);
        Assert.Equal(2, targetRows.Count);

        var importedWeb = targetRows.Single(x => x.Header.Id == webId);
        Assert.True(importedWeb.Header.Favorite);
        Assert.Single(importedWeb.Labels);
        Assert.Equal("Work", importedWeb.Labels[0].Name);

        var webPayload = JsonSerializer.Deserialize<WebPayload>(
            Encoding.UTF8.GetString(VaultPayloadProtector.DecryptItemPayload(targetKey, importedWeb.Header, importedWeb.EncryptedPayload)),
            JsonOptions);

        Assert.NotNull(webPayload);
        Assert.Equal("GitHub", webPayload!.Title);
        Assert.Equal("https://github.com", webPayload.Url);
        Assert.Equal("octocat", webPayload.Username);
        Assert.Equal("secret123", webPayload.Password);
        Assert.Equal("repo access", webPayload.Notes);
    }

    [Fact]
    public async Task PlaintextExport_WritesReadableSnapshotJson()
    {
        using var workspace = new TempWorkspace();
        var repo = new SqliteItemRepository();
        var plaintext = new VaultPlaintextExportService();
        var vaultService = new SqliteVaultService();

        var vaultPath = workspace.FilePath("vault.skvault");
        var exportPath = workspace.FilePath("export.json");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var itemId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");

        await InsertWebAsync(repo, vaultPath, vaultKey, itemId, "GitLab", "https://gitlab.com", "codex", "password!", "notes", createdAt, updatedAt, favorite: false);

        await plaintext.ExportJsonAsync(vaultPath, vaultKey, exportPath);

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
        var csv = new VaultCsvImportService();
        var vaultService = new SqliteVaultService();

        var vaultPath = workspace.FilePath("vault.skvault");
        var csvPath = workspace.FilePath("import.csv");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var existingId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-15).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");
        await InsertWebAsync(repo, vaultPath, vaultKey, existingId, "GitHub", "https://github.com", "octocat", "oldpass", "", createdAt, updatedAt, favorite: false);

        await File.WriteAllTextAsync(csvPath, """
Type,Title,Url,Username,Password,Notes,Content,Cardholder,Number,ExpiryMonth,ExpiryYear,Cvc
Web,GitHub,https://github.com,octocat,newpass,Imported duplicate,,,,,,
Note,Travel,,,,,,Pack passport,,,,,
Card,Card,,,,,,Jane,12345,12,2030,123
""");

        var preview = await csv.PreviewAsync(vaultPath, vaultKey, csvPath);

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
        var csv = new VaultCsvImportService();
        var vaultService = new SqliteVaultService();

        var vaultPath = workspace.FilePath("vault.skvault");
        var csvPath = workspace.FilePath("overwrite.csv");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var itemId = Guid.NewGuid().ToString("N");
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-15).ToString("O");
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");
        await InsertWebAsync(repo, vaultPath, vaultKey, itemId, "GitHub", "https://github.com", "octocat", "oldpass", "", createdAt, updatedAt, favorite: false);

        await File.WriteAllTextAsync(csvPath, """
Type,Title,Url,Username,Password,Notes
Web,GitHub,https://github.com,octocat,newpass,Imported overwrite
""");

        await csv.ImportAsync(vaultPath, vaultKey, csvPath, VaultCsvDuplicateStrategy.OverwriteDuplicates);

        var rows = await repo.ListAsync(vaultPath, vaultKey);
        Assert.Single(rows);

        var payload = JsonSerializer.Deserialize<WebPayload>(
            Encoding.UTF8.GetString(VaultPayloadProtector.DecryptItemPayload(vaultKey, rows[0].Header, rows[0].EncryptedPayload)),
            JsonOptions);

        Assert.NotNull(payload);
        Assert.Equal("newpass", payload!.Password);
        Assert.Equal("Imported overwrite", payload.Notes);
    }

    [Fact]
    public async Task EncryptedImport_RejectsMalformedBase64Package()
    {
        using var workspace = new TempWorkspace();
        var backups = new EncryptedVaultBackupService();
        var packagePath = workspace.FilePath("bad.skbx");

        await File.WriteAllTextAsync(packagePath, JsonSerializer.Serialize(
            new VaultEncryptedPackage(
                Version: 2,
                ExportedAtUtc: DateTimeOffset.UtcNow.ToString("O"),
                Kdf: VaultSecurityProfiles.Default.Kdf,
                SaltBase64: "not-valid-base64",
                CiphertextBase64: "also-not-valid"),
            JsonOptions));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backups.InspectAsync(packagePath, "backup-pass"));

        Assert.Contains("Base64", ex.Message);
    }

    [Fact]
    public async Task EncryptedImport_RejectsTamperedPackageKdfMetadata()
    {
        using var workspace = new TempWorkspace();
        var repo = new SqliteItemRepository();
        var backups = new EncryptedVaultBackupService();
        var vaultService = new SqliteVaultService();
        var vaultPath = workspace.FilePath("vault.skvault");
        var exportPath = workspace.FilePath("backup.skbx");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        await InsertWebAsync(
            repo,
            vaultPath,
            vaultKey,
            Guid.NewGuid().ToString("N"),
            "Portal",
            "https://example.test",
            "user",
            "secret",
            "",
            DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"),
            DateTimeOffset.UtcNow.ToString("O"),
            favorite: false);

        await backups.CreateAsync(vaultPath, vaultKey, exportPath, "backup-pass");

        var package = JsonSerializer.Deserialize<VaultEncryptedPackage>(await File.ReadAllTextAsync(exportPath), JsonOptions)!;
        var tampered = package with { Kdf = package.Kdf with { Iterations = package.Kdf.Iterations + 1 } };
        await File.WriteAllTextAsync(exportPath, JsonSerializer.Serialize(tampered, JsonOptions));

        await Assert.ThrowsAnyAsync<CryptographicException>(() =>
            backups.InspectAsync(exportPath, "backup-pass"));
    }

    [Fact]
    public async Task CsvPreview_RejectsOversizedAndMalformedCsv()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var csv = new VaultCsvImportService();
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var hugeCsv = workspace.FilePath("huge.csv");
        await File.WriteAllTextAsync(hugeCsv, new string('A', 8 * 1024 * 1024 + 1));

        var hugeEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            csv.PreviewAsync(vaultPath, vaultKey, hugeCsv));
        Assert.Contains("too large", hugeEx.Message, StringComparison.OrdinalIgnoreCase);

        var malformedCsv = workspace.FilePath("malformed.csv");
        await File.WriteAllTextAsync(malformedCsv, "Type,Title\nWeb,\"unterminated");

        var malformedEx = await Assert.ThrowsAsync<InvalidDataException>(() =>
            csv.PreviewAsync(vaultPath, vaultKey, malformedCsv));
        Assert.Contains("unterminated", malformedEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAndImport_RejectActiveVaultPath()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var backups = new EncryptedVaultBackupService();
        var plaintext = new VaultPlaintextExportService();
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            plaintext.ExportJsonAsync(vaultPath, vaultKey, vaultPath));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            backups.RestoreAsync(vaultPath, "backup-pass", vaultPath, vaultKey));
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
        string createdAtUtc,
        string updatedAtUtc,
        bool favorite)
    {
        var payload = new WebPayload(title, url, username, password, notes);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var header = new VaultItemHeader(id, ItemType.Web, favorite, createdAtUtc, updatedAtUtc);
        var encrypted = VaultPayloadProtector.EncryptItemPayload(vaultKey, header, json);

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
        var header = new VaultItemHeader(id, ItemType.Note, false, createdAtUtc, updatedAtUtc);
        var encrypted = VaultPayloadProtector.EncryptItemPayload(vaultKey, header, json);

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
