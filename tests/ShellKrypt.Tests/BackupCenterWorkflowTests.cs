using System.Reflection;
using System.Text.Json;
using ShellKrypt.Application.Settings;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Desktop.ViewModels;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;
using ShellKrypt.UI.Shared.Navigation;
using Xunit;

namespace ShellKrypt.Tests;

[Collection(AppRootTestCollection.Name)]
public sealed class BackupCenterWorkflowTests
{
    [Fact]
    public async Task BackupCenter_InitializesSuggestedPaths()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateVaultAsync(vaultPath, "Vault Master Password 2026");

        var backupCenter = CreateUnlockedBackupCenter(vaultPath, vaultKey);

        Assert.EndsWith(".skbx", backupCenter.EncryptedExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".json", backupCenter.PlaintextExportPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DECRYPTED", backupCenter.PlaintextExportPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BackupCenter_ExportVerifyAndRestoreEncryptedBackup()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);
        var vaultService = new SqliteVaultService();

        var sourceVault = workspace.FilePath("source.skvault");
        var targetVault = workspace.FilePath("target.skvault");
        var backupPath = workspace.FilePath("source-backup.skbx");
        var sourceKey = await CreateVaultAsync(vaultService, sourceVault, "Source Master Password 2026");
        var targetKey = await CreateVaultAsync(vaultService, targetVault, "Target Master Password 2026");
        await webLogins.AddAsync(sourceVault, sourceKey, new WebLoginInput("Portal", "https://example.com", "admin", "admin@example.com", "secret-pass", "backup test"));

        var sourceBackup = CreateUnlockedBackupCenter(sourceVault, sourceKey);
        sourceBackup.EncryptedExportPath = backupPath;
        sourceBackup.ExportPassphrase = "backup-passphrase";

        await sourceBackup.PreviewExportCommand.ExecuteAsync(null);
        await sourceBackup.ExportEncryptedCommand.ExecuteAsync(null);

        Assert.Contains("Items: 1", sourceBackup.ExportSummary);
        Assert.True(File.Exists(backupPath));
        Assert.Contains("Encrypted backup saved", sourceBackup.TransferStatus);
        Assert.Contains(sourceBackup.RecentHistory, row => row.OperationLabel == "Encrypted backup");

        var verifier = CreateUnlockedBackupCenter(sourceVault, sourceKey);
        verifier.VerifyBackupPath = backupPath;
        verifier.VerifyPassphrase = "backup-passphrase";

        await verifier.VerifyBackupCommand.ExecuteAsync(null);

        Assert.Contains("Previewing import: 1 items", verifier.VerifySummary);
        Assert.Contains("verified", verifier.TransferStatus, StringComparison.OrdinalIgnoreCase);

        var targetBackup = CreateUnlockedBackupCenter(targetVault, targetKey);
        targetBackup.RestoreBackupPath = backupPath;
        targetBackup.RestorePassphrase = "backup-passphrase";

        await targetBackup.RestoreEncryptedCommand.ExecuteAsync(null);
        Assert.Contains("Confirm restore", targetBackup.TransferStatus);

        targetBackup.ConfirmRestore = true;
        await targetBackup.RestoreEncryptedCommand.ExecuteAsync(null);

        Assert.Contains("Previewing import: 1 items", targetBackup.RestoreSummary);
        var restored = await webLogins.ListAsync(targetVault, targetKey);
        var login = Assert.Single(restored);
        Assert.Equal("Portal", login.Title);
        Assert.Equal("secret-pass", login.Password);
    }

    [Fact]
    public async Task BackupCenter_RequirePlaintextConfirmationAndExportJson()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);

        var vaultPath = workspace.FilePath("vault.skvault");
        var exportPath = workspace.FilePath("decrypted-export.json");
        var vaultKey = await CreateVaultAsync(vaultPath, "Vault Master Password 2026");
        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Plaintext Portal", "https://plain.example", "owner", "owner@example.com", "plaintext-secret", "json export"));

        var backupCenter = CreateUnlockedBackupCenter(vaultPath, vaultKey);
        backupCenter.PlaintextExportPath = exportPath;

        await backupCenter.ExportPlaintextCommand.ExecuteAsync(null);
        Assert.False(File.Exists(exportPath));
        Assert.Contains("Confirm the plaintext export warning", backupCenter.TransferStatus);

        backupCenter.ConfirmPlaintextExport = true;
        backupCenter.PlaintextExportConfirmationText = "NOPE";
        await backupCenter.ExportPlaintextCommand.ExecuteAsync(null);
        Assert.False(File.Exists(exportPath));
        Assert.Contains("Type EXPORT", backupCenter.TransferStatus);

        backupCenter.PlaintextExportConfirmationText = "EXPORT";
        await backupCenter.ExportPlaintextCommand.ExecuteAsync(null);

        Assert.True(File.Exists(exportPath));
        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("Plaintext Portal", json);
        Assert.Contains("plaintext-secret", json);
        Assert.False(backupCenter.ConfirmPlaintextExport);
        Assert.Equal("", backupCenter.PlaintextExportConfirmationText);
        Assert.Contains("decrypted", backupCenter.TransferStatus);
    }

    [Fact]
    public async Task BackupCenter_PreviewAndImportCsv()
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

        var backupCenter = CreateUnlockedBackupCenter(vaultPath, vaultKey);
        backupCenter.CsvImportPath = csvPath;
        backupCenter.SelectedCsvDuplicateStrategy = VaultCsvDuplicateStrategy.ImportAll;

        await backupCenter.PreviewCsvImportCommand.ExecuteAsync(null);
        await backupCenter.ImportCsvCommand.ExecuteAsync(null);

        Assert.Equal("Rows: 1 | New: 1 | Duplicates: 0 | Invalid: 0", backupCenter.CsvPreviewSummary);
        var row = Assert.Single(backupCenter.CsvPreviewRows);
        Assert.Equal(VaultCsvRowStatus.New, row.Status);
        var imported = await webLogins.ListAsync(vaultPath, vaultKey);
        var login = Assert.Single(imported);
        Assert.Equal("Imported Portal", login.Title);
        Assert.Equal("csv-secret", login.Password);
    }

    [Fact]
    public async Task BackupCenter_RunAutomaticBackupNow_ExportsVerifiesAndDoesNotPersistPassphrase()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var repo = new SqliteItemRepository();
        var webLogins = new WebLoginService(repo);

        var vaultPath = workspace.FilePath("vault.skvault");
        var backupDir = workspace.FilePath("auto-backups");
        Directory.CreateDirectory(backupDir);
        var vaultKey = await CreateVaultAsync(vaultPath, "Vault Master Password 2026");
        await webLogins.AddAsync(vaultPath, vaultKey, new WebLoginInput("Auto Portal", "https://auto.example", "owner", "", "auto-secret", ""));

        var backupCenter = CreateUnlockedBackupCenter(vaultPath, vaultKey);
        backupCenter.AutomaticBackupEnabled = true;
        backupCenter.AutomaticBackupDirectory = backupDir;
        backupCenter.AutomaticBackupPassphrase = "automatic-backup-passphrase";
        backupCenter.AutomaticBackupRetentionCount = 2;

        await backupCenter.RunAutomaticBackupNowCommand.ExecuteAsync(null);

        var backupPath = Assert.Single(Directory.GetFiles(backupDir, "*.skbx"));
        Assert.Contains("ShellKrypt-vault-Auto-", Path.GetFileName(backupPath));
        Assert.Contains("Automatic backup completed", backupCenter.AutomaticBackupStatus);
        Assert.Contains(backupCenter.RecentHistory, row => row.OperationLabel == "Automatic backup");

        var settingsJson = File.Exists(DefaultPaths.SettingsPath)
            ? File.ReadAllText(DefaultPaths.SettingsPath)
            : "";
        Assert.DoesNotContain("automatic-backup-passphrase", settingsJson);
        Assert.DoesNotContain("auto-secret", settingsJson);
    }

    [Fact]
    public async Task BackupCenter_AutomaticBackupRequiresSessionPassphrase()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));

        var vaultPath = workspace.FilePath("vault.skvault");
        var backupDir = workspace.FilePath("auto-backups");
        Directory.CreateDirectory(backupDir);
        var vaultKey = await CreateVaultAsync(vaultPath, "Vault Master Password 2026");

        var backupCenter = CreateUnlockedBackupCenter(vaultPath, vaultKey);
        backupCenter.AutomaticBackupEnabled = true;
        backupCenter.AutomaticBackupDirectory = backupDir;

        await backupCenter.RunAutomaticBackupNowCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFiles(backupDir, "*.skbx"));
        Assert.Contains("passphrase", backupCenter.AutomaticBackupStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AutomaticBackupRetention_DeletesOnlyMatchingAutoBackupFiles()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("vault.skvault");
        var backupDir = workspace.FilePath("auto-backups");
        Directory.CreateDirectory(backupDir);

        var older = AutomaticBackupCoordinator.BuildBackupPath(backupDir, vaultPath, new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero));
        var middle = AutomaticBackupCoordinator.BuildBackupPath(backupDir, vaultPath, new DateTimeOffset(2026, 5, 2, 10, 0, 0, TimeSpan.Zero));
        var newer = AutomaticBackupCoordinator.BuildBackupPath(backupDir, vaultPath, new DateTimeOffset(2026, 5, 3, 10, 0, 0, TimeSpan.Zero));
        var manual = Path.Combine(backupDir, "ShellKrypt-vault-Manual.skbx");
        var otherVaultAuto = Path.Combine(backupDir, "ShellKrypt-other-Auto-20260501-100000.skbx");

        File.WriteAllText(older, "old");
        File.WriteAllText(middle, "mid");
        File.WriteAllText(newer, "new");
        File.WriteAllText(manual, "manual");
        File.WriteAllText(otherVaultAuto, "other");
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(middle, new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newer, new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc));

        var deleted = AutomaticBackupCoordinator.ApplyRetention(backupDir, vaultPath, retentionCount: 2);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(older));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newer));
        Assert.True(File.Exists(manual));
        Assert.True(File.Exists(otherVaultAuto));
    }

    [Fact]
    public async Task AutomaticBackupCoordinator_CheckDueSkipsWithoutSessionPassphrase()
    {
        using var workspace = new TempWorkspace();
        var transfer = new FakeVaultTransferService();
        var schedule = new BackupScheduleSettings
        {
            Enabled = true,
            BackupDirectory = workspace.Root,
            Frequency = BackupScheduleFrequency.Daily
        };
        var state = new AutomaticBackupState();
        var coordinator = new AutomaticBackupCoordinator(
            transfer,
            () => new AutomaticBackupContext(workspace.FilePath("vault.skvault"), [1, 2, 3], schedule, state));

        var result = await coordinator.CheckDueAsync();

        Assert.Null(result);
        Assert.Equal(0, transfer.ExportEncryptedCallCount);
        Assert.Equal("", state.LastAttemptedAtUtc);
    }

    [Fact]
    public async Task BackupCenter_BackupHealthReflectsBackupVerificationAndAutomaticState()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateVaultAsync(vaultPath, "Vault Master Password 2026");
        var root = new MainWindowViewModel();
        root.SetVaultPath(vaultPath);
        root.BackupCenterHistory.AddEntry(new BackupCenterHistoryEntry
        {
            Operation = "encrypted-backup",
            Status = "success",
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            VaultName = "Vault",
            FileName = "safe-backup.skbx",
            FullPath = workspace.FilePath("safe-backup.skbx")
        });
        root.BackupCenterHistory.AddEntry(new BackupCenterHistoryEntry
        {
            Operation = "verify-backup",
            Status = "success",
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            VaultName = "Vault",
            FileName = "safe-backup.skbx",
            FullPath = workspace.FilePath("safe-backup.skbx")
        });
        root.BackupSchedule.Enabled = true;
        root.BackupSchedule.BackupDirectory = workspace.FilePath("auto-backups");
        root.AutomaticBackupState.LastSuccessfulAtUtc = DateTimeOffset.UtcNow.ToString("O");
        root.AutomaticBackupState.LastVerifiedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        root.AutomaticBackupState.LastBackupFileName = "auto-backup.skbx";
        root.OnUnlocked(vaultKey);
        root.SetAutomaticBackupSessionPassphrase("do-not-store-backup-passphrase");

        var shell = Assert.IsType<ShellViewModel>(root.Current);
        var backupCenter = shell.BackupCenter;
        var settingsJson = File.Exists(DefaultPaths.SettingsPath)
            ? File.ReadAllText(DefaultPaths.SettingsPath)
            : "";

        Assert.Equal("Created", backupCenter.BackupHealthBackupStatus);
        Assert.Equal("Verified", backupCenter.BackupHealthVerificationStatus);
        Assert.Equal("Enabled", backupCenter.BackupHealthAutomaticStatus);
        Assert.Contains("safe-backup.skbx", backupCenter.BackupHealthBackupDetail);
        Assert.Contains("safe-backup.skbx", backupCenter.BackupHealthVerificationDetail);
        Assert.Contains("auto-backup.skbx", backupCenter.BackupHealthAutomaticDetail);
        Assert.DoesNotContain("do-not-store-backup-passphrase", settingsJson);
        Assert.DoesNotContain("Vault Master Password 2026", settingsJson);
    }

    [Fact]
    public void Sidebar_NoLongerContainsEmergencyKitSection()
    {
        Assert.DoesNotContain(ShellKryptSectionCatalog.DesktopSections, section => section.Key == "emergency");
        Assert.Contains(ShellKryptSectionCatalog.DesktopSections, section => section.Key == ShellKryptSectionKeys.Backup);
    }

    [Fact]
    public void BackupCenterHistory_PersistsLimitsAndDoesNotStoreSecrets()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var service = new AppSettingsService(new FileAppSettingsStore());
        var settings = service.Load();

        for (var i = 0; i < 12; i++)
        {
            settings.BackupCenterHistory.AddEntry(new BackupCenterHistoryEntry
            {
                Operation = "encrypted-backup",
                Status = "success",
                TimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-i).ToString("O"),
                VaultName = "Vault",
                FileName = $"backup-{i}.skbx",
                FullPath = workspace.FilePath($"backup-{i}.skbx"),
                ItemCount = i,
                LabelCount = i
            });
        }

        settings.BackupCenterHistory.LastEncryptedBackupPath = workspace.FilePath("latest.skbx");
        service.Save(settings);

        var loaded = service.Load();
        var json = File.ReadAllText(DefaultPaths.SettingsPath);

        Assert.Equal(BackupCenterHistory.MaxRecentEntries, loaded.BackupCenterHistory.RecentEntries.Count);
        Assert.Equal(workspace.FilePath("latest.skbx"), loaded.BackupCenterHistory.LastEncryptedBackupPath);
        Assert.DoesNotContain("backup-passphrase", json);
        Assert.DoesNotContain("plaintext-secret", json);
        Assert.DoesNotContain("4111111111111111", json);
    }

    [Fact]
    public void SettingsViewModel_NoLongerOwnsTransferWorkflowCommands()
    {
        var names = typeof(SettingsViewModel)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("ExportEncryptedCommand", names);
        Assert.DoesNotContain("ImportEncryptedCommand", names);
        Assert.DoesNotContain("ExportPlaintextCommand", names);
        Assert.DoesNotContain("ImportCsvCommand", names);
    }

    private static async Task<byte[]> CreateVaultAsync(string vaultPath, string masterPassword)
        => await CreateVaultAsync(new SqliteVaultService(), vaultPath, masterPassword);

    private static async Task<byte[]> CreateVaultAsync(SqliteVaultService vaultService, string vaultPath, string masterPassword)
    {
        await vaultService.CreateAsync(vaultPath, masterPassword);
        var unlock = await vaultService.UnlockAsync(vaultPath, masterPassword);
        Assert.True(unlock.Success, unlock.Error);
        return unlock.VaultKey ?? throw new InvalidOperationException("Vault key was not returned.");
    }

    private static BackupCenterViewModel CreateUnlockedBackupCenter(string vaultPath, byte[] vaultKey)
    {
        var root = new MainWindowViewModel();
        root.SetVaultPath(vaultPath);
        root.OnUnlocked(vaultKey);
        return Assert.IsType<ShellViewModel>(root.Current).BackupCenter;
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
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.BackupCenter.Tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FakeVaultTransferService : IVaultTransferService
    {
        public int ExportEncryptedCallCount { get; private set; }

        public Task<VaultSnapshotSummary> GetExportSummaryAsync(string vaultPath, byte[] vaultKey, CancellationToken ct = default)
            => Task.FromResult(new VaultSnapshotSummary(0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task ExportPlaintextJsonAsync(string vaultPath, byte[] vaultKey, string outputPath, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task ExportEncryptedAsync(string vaultPath, byte[] vaultKey, string outputPath, string exportPassphrase, CancellationToken ct = default)
        {
            ExportEncryptedCallCount++;
            return Task.CompletedTask;
        }

        public Task<VaultSnapshotSummary> GetEncryptedImportSummaryAsync(string packagePath, string exportPassphrase, CancellationToken ct = default)
            => Task.FromResult(new VaultSnapshotSummary(0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task ImportEncryptedAsync(string packagePath, string exportPassphrase, string vaultPath, byte[] vaultKey, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task ImportSnapshotAsync(string vaultPath, byte[] vaultKey, VaultSnapshot snapshot, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<VaultCsvImportPreview> PreviewCsvImportAsync(string vaultPath, byte[] vaultKey, string csvPath, CancellationToken ct = default)
            => Task.FromResult(new VaultCsvImportPreview(0, 0, 0, 0, []));

        public Task ImportCsvAsync(string vaultPath, byte[] vaultKey, string csvPath, VaultCsvDuplicateStrategy strategy, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
