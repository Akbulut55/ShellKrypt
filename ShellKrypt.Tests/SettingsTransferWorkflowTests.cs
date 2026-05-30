using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class SettingsTransferWorkflowTests
{
    [Fact]
    public async Task SettingsTransferCommands_ExportAndRestoreEncryptedBackup()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);

        var sourceVault = workspace.FilePath("source.skvault");
        var targetVault = workspace.FilePath("target.skvault");
        var backupPath = workspace.FilePath("source-backup.skbx");
        var sourceKey = await CreateVaultAsync(sourceVault, "Source Master Password 2026");
        var targetKey = await CreateVaultAsync(targetVault, "Target Master Password 2026");
        await webLogins.AddAsync(sourceVault, sourceKey, new WebLoginInput("Portal", "https://example.com", "admin", "admin@example.com", "secret-pass", "backup test"));

        var sourceSettings = CreateUnlockedSettings(sourceVault, sourceKey);
        sourceSettings.EncryptedExportPath = backupPath;
        sourceSettings.ExportPassphrase = "backup-passphrase";

        await sourceSettings.PreviewExportCommand.ExecuteAsync(null);
        await sourceSettings.ExportEncryptedCommand.ExecuteAsync(null);

        Assert.Contains("Items: 1", sourceSettings.ExportSummary);
        Assert.True(File.Exists(backupPath));
        Assert.Contains("Encrypted backup saved", sourceSettings.TransferStatus);

        var targetSettings = CreateUnlockedSettings(targetVault, targetKey);
        targetSettings.EncryptedImportPath = backupPath;
        targetSettings.EncryptedImportPassphrase = "backup-passphrase";

        await targetSettings.PreviewEncryptedImportCommand.ExecuteAsync(null);
        await targetSettings.ImportEncryptedCommand.ExecuteAsync(null);

        Assert.Contains("Previewing import: 1 items", targetSettings.EncryptedImportSummary);
        var restored = await webLogins.ListAsync(targetVault, targetKey);
        var login = Assert.Single(restored);
        Assert.Equal("Portal", login.Title);
        Assert.Equal("secret-pass", login.Password);
    }

    [Fact]
    public async Task SettingsTransferCommands_RequirePlaintextConfirmationAndExportJson()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);

        var vaultPath = workspace.FilePath("vault.skvault");
        var exportPath = workspace.FilePath("decrypted-export.json");
        var vaultKey = await CreateVaultAsync(vaultPath, "Vault Master Password 2026");
        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Plaintext Portal", "https://plain.example", "owner", "owner@example.com", "plaintext-secret", "json export"));

        var settings = CreateUnlockedSettings(vaultPath, vaultKey);
        settings.PlaintextExportPath = exportPath;

        await settings.ExportPlaintextCommand.ExecuteAsync(null);
        Assert.False(File.Exists(exportPath));
        Assert.Contains("Confirm the plaintext export warning", settings.TransferStatus);

        settings.ConfirmPlaintextExport = true;
        settings.PlaintextExportConfirmationText = "NOPE";
        await settings.ExportPlaintextCommand.ExecuteAsync(null);
        Assert.False(File.Exists(exportPath));
        Assert.Contains("Type EXPORT", settings.TransferStatus);

        settings.PlaintextExportConfirmationText = "EXPORT";
        await settings.ExportPlaintextCommand.ExecuteAsync(null);

        Assert.True(File.Exists(exportPath));
        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("Plaintext Portal", json);
        Assert.Contains("plaintext-secret", json);
        Assert.False(settings.ConfirmPlaintextExport);
        Assert.Equal("", settings.PlaintextExportConfirmationText);
        Assert.Contains("decrypted", settings.TransferStatus);
    }

    [Fact]
    public async Task SettingsTransferCommands_PreviewAndImportCsv()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);

        var vaultPath = workspace.FilePath("vault.skvault");
        var csvPath = workspace.FilePath("import.csv");
        var vaultKey = await CreateVaultAsync(vaultPath, "Vault Master Password 2026");
        await File.WriteAllTextAsync(csvPath, """
Type,Title,Url,Username,Email,Password,Notes
Web,Imported Portal,https://import.example,importer,importer@example.com,csv-secret,from csv
""");

        var settings = CreateUnlockedSettings(vaultPath, vaultKey);
        settings.CsvImportPath = csvPath;
        settings.SelectedCsvDuplicateStrategy = VaultCsvDuplicateStrategy.ImportAll;

        await settings.PreviewCsvImportCommand.ExecuteAsync(null);
        await settings.ImportCsvCommand.ExecuteAsync(null);

        Assert.Equal("Rows: 1 | New: 1 | Duplicates: 0 | Invalid: 0", settings.CsvPreviewSummary);
        var row = Assert.Single(settings.CsvPreviewRows);
        Assert.Equal(VaultCsvRowStatus.New, row.Status);
        var imported = await webLogins.ListAsync(vaultPath, vaultKey);
        var login = Assert.Single(imported);
        Assert.Equal("Imported Portal", login.Title);
        Assert.Equal("csv-secret", login.Password);
    }

    private static async Task<byte[]> CreateVaultAsync(string vaultPath, string masterPassword)
    {
        var vaultService = new SqliteVaultService();
        await vaultService.CreateAsync(vaultPath, masterPassword);
        var unlock = await vaultService.UnlockAsync(vaultPath, masterPassword);
        Assert.True(unlock.Success, unlock.Error);
        return unlock.VaultKey ?? throw new InvalidOperationException("Vault key was not returned.");
    }

    private static SettingsViewModel CreateUnlockedSettings(string vaultPath, byte[] vaultKey)
    {
        var root = new MainWindowViewModel();
        root.SetVaultPath(vaultPath);
        root.OnUnlocked(vaultKey);
        return Assert.IsType<ShellViewModel>(root.Current).Settings;
    }

    private sealed class AppRootScope : IDisposable
    {
        private readonly string? _previousRoot;

        public AppRootScope(string appRoot)
        {
            _previousRoot = Environment.GetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable);
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, appRoot);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, _previousRoot);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.SettingsTransfer.Tests", Guid.NewGuid().ToString("N"));
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
