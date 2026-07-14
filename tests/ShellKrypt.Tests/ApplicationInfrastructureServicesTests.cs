using System.Text.Json;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Localization;
using ShellKrypt.Application.Markdown;
using ShellKrypt.Application.Settings;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

[Collection(AppRootTestCollection.Name)]
public sealed class ApplicationInfrastructureServicesTests
{
    [Fact]
    public void AppSettingsService_PreservesDesktopDefaultsAndJsonShape()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));

        var service = new AppSettingsService(new FileAppSettingsStore());
        var settings = service.Load();

        Assert.Equal(AppSettings.DefaultThemeId, settings.ThemeId);
        Assert.Equal(AppSettings.DefaultLanguageId, settings.LanguageId);
        Assert.True(settings.AutoLockEnabled);
        Assert.Equal(15, settings.AutoLockMinutes);
        Assert.Equal(20, settings.LockOnDeactivateSeconds);
        Assert.Equal(15, settings.ClipboardClearSeconds);
        Assert.False(settings.CloseToTrayEnabled);
        Assert.Null(settings.SecurityAcknowledgementAcceptedAtUtc);
        Assert.Equal(0, settings.SecurityAcknowledgementVersionAccepted);
        Assert.False(settings.HasCurrentSecurityAcknowledgement);
        Assert.NotNull(settings.BackupCenterHistory);
        Assert.Empty(settings.BackupCenterHistory.RecentEntries);
        Assert.NotNull(settings.EmergencyKit);
        Assert.NotNull(settings.BackupSchedule);
        Assert.NotNull(settings.AutomaticBackupState);
        Assert.NotNull(settings.QuickFill);
        Assert.False(settings.BackupSchedule.Enabled);
        Assert.Equal(BackupScheduleSettings.DefaultRetentionCount, settings.BackupSchedule.RetentionCount);
        Assert.True(settings.QuickFill.GlobalHotkeyEnabled);
        Assert.Equal(QuickFillSettings.DefaultShortcut, settings.QuickFill.GlobalShortcut);

        settings.ClipboardClearSeconds = 1;
        settings.ClipboardCopyEnabled = false;
        settings.CloseToTrayEnabled = true;
        settings.ThemeId = "light";
        settings.LanguageId = "tr";
        settings.BackupCenterHistory.LastEncryptedBackupPath = workspace.FilePath("backup.skbx");
        settings.BackupSchedule.Enabled = true;
        settings.BackupSchedule.BackupDirectory = workspace.Root;
        settings.BackupSchedule.Frequency = BackupScheduleFrequency.Weekly;
        settings.BackupSchedule.RetentionCount = 7;
        settings.AutomaticBackupState.LastStatus = "success";
        settings.QuickFill.GlobalShortcut = " Ctrl+Alt+K ";
        settings.QuickFill.AutoTypeAcknowledgedAtUtc = " 2026-06-16T10:00:00.0000000+00:00 ";
        settings.EmergencyKit.NoPasswordRecoveryAcknowledged = true;
        settings.AcceptCurrentSecurityAcknowledgement("2026-05-31T10:15:30.0000000+00:00");
        service.Save(settings);

        var json = File.ReadAllText(DefaultPaths.SettingsPath);
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.ThemeId), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.LanguageId), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.ClipboardClearSeconds), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.CloseToTrayEnabled), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.SecurityAcknowledgementAcceptedAtUtc), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.SecurityAcknowledgementVersionAccepted), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.BackupCenterHistory), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.EmergencyKit), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.BackupSchedule), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.AutomaticBackupState), out _));
        Assert.True(document.RootElement.TryGetProperty(nameof(AppSettings.QuickFill), out _));
        Assert.Equal(SessionSecuritySettings.MinClipboardClearSeconds, service.Load().ClipboardClearSeconds);
        Assert.False(service.Load().ClipboardCopyEnabled);
        Assert.True(service.Load().CloseToTrayEnabled);
        Assert.Equal("light", service.Load().ThemeId);
        Assert.Equal("tr", service.Load().LanguageId);
        Assert.Equal(workspace.FilePath("backup.skbx"), service.Load().BackupCenterHistory.LastEncryptedBackupPath);
        Assert.True(service.Load().BackupSchedule.Enabled);
        Assert.Equal(BackupScheduleFrequency.Weekly, service.Load().BackupSchedule.Frequency);
        Assert.Equal(7, service.Load().BackupSchedule.RetentionCount);
        Assert.Equal(QuickFillSettings.DefaultShortcut, service.Load().QuickFill.GlobalShortcut);
        Assert.Equal("2026-06-16T10:00:00.0000000+00:00", service.Load().QuickFill.AutoTypeAcknowledgedAtUtc);
        Assert.True(service.Load().EmergencyKit.NoPasswordRecoveryAcknowledged);
        Assert.Equal("2026-05-31T10:15:30.0000000+00:00", service.Load().SecurityAcknowledgementAcceptedAtUtc);
        Assert.Equal(AppSettings.CurrentSecurityAcknowledgementVersion, service.Load().SecurityAcknowledgementVersionAccepted);
        Assert.True(service.Load().HasCurrentSecurityAcknowledgement);
    }

    [Theory]
    [InlineData("{\"ThemeId\":\"forest\"}", "dark")]
    [InlineData("{\"ThemeId\":\"unknown\"}", "dark")]
    [InlineData("{\"ThemeId\":\"\"}", "dark")]
    public void AppSettingsService_NormalizesThemeStorage(string json, string expectedThemeId)
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultPaths.SettingsPath)!);
        File.WriteAllText(DefaultPaths.SettingsPath, json);

        var service = new AppSettingsService(new FileAppSettingsStore());
        var settings = service.Load();
        service.Save(settings);

        Assert.Equal(expectedThemeId, settings.ThemeId);

        using var saved = JsonDocument.Parse(File.ReadAllText(DefaultPaths.SettingsPath));
        Assert.Equal(expectedThemeId, saved.RootElement.GetProperty(nameof(AppSettings.ThemeId)).GetString());
    }

    [Theory]
    [InlineData("{\"LanguageId\":\"tr\"}", "tr")]
    [InlineData("{\"LanguageId\":\"unknown\"}", "en")]
    [InlineData("{\"LanguageId\":\"\"}", "en")]
    [InlineData("{}", "en")]
    public void AppSettingsService_NormalizesLanguageStorage(string json, string expectedLanguageId)
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultPaths.SettingsPath)!);
        File.WriteAllText(DefaultPaths.SettingsPath, json);

        var service = new AppSettingsService(new FileAppSettingsStore());
        var settings = service.Load();
        service.Save(settings);

        Assert.Equal(expectedLanguageId, settings.LanguageId);

        using var saved = JsonDocument.Parse(File.ReadAllText(DefaultPaths.SettingsPath));
        Assert.Equal(expectedLanguageId, saved.RootElement.GetProperty(nameof(AppSettings.LanguageId)).GetString());
    }

    [Fact]
    public void AppSettings_RequiresCurrentSecurityAcknowledgementVersion()
    {
        var settings = new AppSettings
        {
            SecurityAcknowledgementAcceptedAtUtc = "2026-05-31T10:15:30.0000000+00:00",
            SecurityAcknowledgementVersionAccepted = AppSettings.CurrentSecurityAcknowledgementVersion - 1
        };

        Assert.False(settings.HasCurrentSecurityAcknowledgement);

        settings.AcceptCurrentSecurityAcknowledgement("2026-05-31T10:16:30.0000000+00:00");

        Assert.True(settings.HasCurrentSecurityAcknowledgement);
        Assert.Equal(AppSettings.CurrentSecurityAcknowledgementVersion, settings.SecurityAcknowledgementVersionAccepted);
    }

    [Fact]
    public void AppSettingsService_NormalizesBackupScheduleAndEmergencyState()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultPaths.SettingsPath)!);
        File.WriteAllText(DefaultPaths.SettingsPath, """
        {
          "BackupSchedule": {
            "Enabled": true,
            "BackupDirectory": "  C:\\Backups  ",
            "Frequency": 999,
            "RetentionCount": 999
          },
          "AutomaticBackupState": {
            "LastStatus": " SUCCESS "
          },
          "EmergencyKit": {
            "LastChecklistExportPath": "  kit.txt  "
          }
        }
        """);

        var service = new AppSettingsService(new FileAppSettingsStore());
        var settings = service.Load();
        service.Save(settings);

        Assert.True(settings.BackupSchedule.Enabled);
        Assert.Equal("C:\\Backups", settings.BackupSchedule.BackupDirectory);
        Assert.Equal(BackupScheduleFrequency.Daily, settings.BackupSchedule.Frequency);
        Assert.Equal(BackupScheduleSettings.MaxRetentionCount, settings.BackupSchedule.RetentionCount);
        Assert.Equal("success", settings.AutomaticBackupState.LastStatus);
        Assert.Equal("kit.txt", settings.EmergencyKit.LastChecklistExportPath);
    }

    [Fact]
    public void VaultRegistryService_NormalizesFavoritesRecentOrderingAndDuplicates()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var service = new VaultRegistryService(new FileVaultRegistryStore());
        var older = workspace.FilePath("older.skvault");
        var newer = workspace.FilePath("newer.skvault");

        service.UpsertVault(older, "Older", "Favorite vault");
        service.UpsertVault(older, "Older duplicate", "duplicate");
        service.SetVaultFavorite(older, true);
        service.UpsertVault(newer, "Newer", "", markOpened: true);

        var vaults = service.ListVaults();

        Assert.Equal(2, vaults.Count);
        Assert.Equal("Older duplicate", vaults[0].DisplayName);
        Assert.True(vaults[0].IsFavorite);
        Assert.NotNull(vaults.Single(vault => vault.VaultPath == older).Id);
        Assert.Equal("duplicate", vaults.Single(vault => vault.VaultPath == older).Description);
        Assert.Equal("Newer", service.ListRecentVaults().Single().DisplayName);
    }

    [Fact]
    public async Task ActivityLogService_SanitizesAndPersistsEncryptedVaultScopedEntries()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("activity.skvault");
        var vaultService = new SqliteVaultService();
        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");
        var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
        Assert.True(unlock.Success, unlock.Error);

        var service = new ActivityLogService(new SqliteActivityLogStore());
        service.Append(
            new ActivityLogEntry(
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow.ToString("O"),
                "test",
                "Sensitive",
                "password=super-secret cvc=123 4111111111111111",
                "warning",
                vaultPath),
            unlock.VaultKey);

        var loaded = Assert.Single(service.Load(vaultPath, unlock.VaultKey));
        Assert.DoesNotContain("super-secret", loaded.Detail);
        Assert.DoesNotContain("4111111111111111", loaded.Detail);
        Assert.Contains("[redacted]", loaded.Detail);
    }

    [Fact]
    public void AuditDismissalService_LoadsFingerprintsPerVault()
    {
        using var workspace = new TempWorkspace();
        using var appRoot = new AppRootScope(workspace.FilePath("appdata"));
        var service = new AuditDismissalService(new FileDismissedAuditIssueStore());
        var vaultA = workspace.FilePath("a.skvault");
        var vaultB = workspace.FilePath("b.skvault");

        service.Dismiss(vaultA, "fingerprint-a");
        service.Dismiss(vaultB, "fingerprint-b");

        Assert.Contains("fingerprint-a", service.LoadFingerprints(vaultA));
        Assert.DoesNotContain("fingerprint-b", service.LoadFingerprints(vaultA));
    }

    [Fact]
    public void SimpleMarkdown_ParsesBlocksAndPlainText()
    {
        var blocks = SimpleMarkdown.Parse("""
        # Title

        Body with **strong** and [link](https://example.com).

        - one
        - two
        """);

        Assert.Contains(blocks, block => block.IsHeading1 && block.Text == "Title");
        Assert.Contains(blocks, block => block.IsList && block.DisplayItems.Count == 2);
        Assert.Equal("Title Body with strong and link. one two", SimpleMarkdown.ToPlainText("# Title\n\nBody with **strong** and [link](https://example.com).\n\n- one\n- two"));
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
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.ApplicationInfrastructure.Tests", Guid.NewGuid().ToString("N"));
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
