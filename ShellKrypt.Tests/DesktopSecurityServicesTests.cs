using System.Security.Cryptography;
using System.Text;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class DesktopSecurityServicesTests
{
    [Fact]
    public void SessionSecuritySettings_EnforcesMinimumClipboardTimeoutAndCopyToggle()
    {
        var normalized = new SessionSecuritySettings
        {
            ClipboardClearSeconds = 1,
            ClipboardCopyEnabled = false
        }.Normalize();

        Assert.Equal(SessionSecuritySettings.MinClipboardClearSeconds, normalized.ClipboardClearSeconds);
        Assert.False(normalized.ClipboardCopyEnabled);
    }

    [Fact]
    public async Task AppMetadata_DoesNotPersistKnownSecretValues()
    {
        using var workspace = new TempWorkspace();
        var previousRoot = Environment.GetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable);
        Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, workspace.FilePath("appdata"));

        try
        {
            var secret = "KnownSecretValue-Do-Not-Persist";
            var vaultService = new SqliteVaultService();
            var repo = new SqliteItemRepository();
            var web = new WebLoginService(repo);
            var vaultPath = DefaultPaths.GetSuggestedVaultPath("metadata-test");

            await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");
            var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
            Assert.True(unlock.Success, unlock.Error);
            Assert.NotNull(unlock.VaultKey);

            await web.AddAsync(vaultPath, unlock.VaultKey!, new WebLoginInput("Metadata Test", "https://example.test", "user", "", secret, "notes"));

            new AppSettingsStore().Save(new AppSettings { ClipboardClearSeconds = 1, ClipboardCopyEnabled = false });
            new VaultRegistryStore().UpsertVault(vaultPath, "Metadata Test", "Local metadata only", isDefault: true);
            new DismissedAuditIssueStore().Dismiss(vaultPath, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))));
            new ActivityLogStore().Append(
                new ActivityLogEntry(
                    Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow.ToString("O"),
                    "web",
                    "Web login password copied",
                    "Copied password for Metadata Test.",
                    "info",
                    vaultPath)
                {
                    AffectedItem = "Metadata Test"
                },
                unlock.VaultKey);

            var secretBytes = Encoding.UTF8.GetBytes(secret);
            foreach (var file in Directory.EnumerateFiles(DefaultPaths.AppRoot, "*", SearchOption.AllDirectories))
            {
                var bytes = await File.ReadAllBytesAsync(file);
                Assert.False(Contains(bytes, secretBytes), $"{file} contains the known secret.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, previousRoot);
        }
    }

    [Fact]
    public async Task ActivityLogStore_RedactsLabeledSecretMaterial()
    {
        using var workspace = new TempWorkspace();
        var previousRoot = Environment.GetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable);
        Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, workspace.FilePath("appdata"));

        try
        {
            var vaultKey = RandomNumberGenerator.GetBytes(32);
            var vaultPath = workspace.FilePath("logs.skvault");
            await new SqliteVaultService().CreateAsync(vaultPath, "Vault Master Passphrase 2026");
            var store = new ActivityLogStore();

            store.Append(
                new ActivityLogEntry(
                    Guid.NewGuid().ToString("N"),
                    DateTimeOffset.UtcNow.ToString("O"),
                    "test",
                    "Sanitize",
                    "password=super-secret cvc=123 4111111111111111",
                    "info",
                    vaultPath),
                vaultKey);

            var loaded = store.Load(vaultPath, vaultKey).Single();

            Assert.DoesNotContain("super-secret", loaded.Detail);
            Assert.DoesNotContain("4111111111111111", loaded.Detail);
            Assert.Contains("[redacted]", loaded.Detail);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DefaultPaths.AppRootOverrideEnvironmentVariable, previousRoot);
        }
    }

    [Fact]
    public void AuthenticatorQrImportService_RejectsCorruptedImage()
    {
        using var workspace = new TempWorkspace();
        var imagePath = workspace.FilePath("corrupt.png");
        File.WriteAllBytes(imagePath, Encoding.UTF8.GetBytes("not an image"));

        var service = new AuthenticatorQrImportService();

        Assert.ThrowsAny<Exception>(() => service.ImportFromImage(imagePath));
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var found = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] == needle[j])
                    continue;

                found = false;
                break;
            }

            if (found)
                return true;
        }

        return false;
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
