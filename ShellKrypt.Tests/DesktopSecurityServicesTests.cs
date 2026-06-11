using System.Security.Cryptography;
using System.Text;
using ShellKrypt.Application.Activity;
using ShellKrypt.Application.Audit;
using ShellKrypt.Application.Settings;
using ShellKrypt.Application.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Desktop.Services;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Services;
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
            var cardNumber = "4111111111111111";
            var cvc = "987";
            var apiSecret = "sk-live-known-secret-value";
            var otpSeed = "JBSWY3DPEHPK3PXP";
            var noteContent = "Private note body that must stay encrypted";
            var vaultService = new SqliteVaultService();
            var repo = new SqliteItemRepository();
            var web = new WebLoginService(repo);
            var cards = new CardService(repo);
            var apiKeys = new ApiKeyService(repo);
            var authenticators = new AuthenticatorService(repo);
            var notes = new NoteService(repo);
            var vaultPath = DefaultPaths.GetSuggestedVaultPath("metadata-test");

            await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");
            var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
            Assert.True(unlock.Success, unlock.Error);
            Assert.NotNull(unlock.VaultKey);

            await web.AddAsync(vaultPath, unlock.VaultKey!, new WebLoginInput("Metadata Test", "https://example.test", "user", "", secret, "notes"));
            await cards.AddAsync(vaultPath, unlock.VaultKey!, new CardInput("Card Test", "Bank", "Holder", cardNumber, 12, 2031, cvc, "card notes", "Visa", "Credit"));
            await apiKeys.AddAsync(
                vaultPath,
                unlock.VaultKey!,
                new ApiKeyInput(
                    "API Test",
                    "Provider",
                    "Production",
                    "api notes",
                    [new ApiKeyFieldInput("field-1", "API Key", "API Key", apiSecret, true, true, 0)]));
            await authenticators.AddAsync(vaultPath, unlock.VaultKey!, new AuthenticatorInput("OTP Test", otpSeed, AuthenticatorKeyType.TimeBased));
            await notes.AddAsync(vaultPath, unlock.VaultKey!, new NoteInput("Note Test", noteContent, Favorite: false));

            new AppSettingsService(new FileAppSettingsStore()).Save(new AppSettings { ClipboardClearSeconds = 1, ClipboardCopyEnabled = false });
            new VaultRegistryService(new FileVaultRegistryStore()).UpsertVault(vaultPath, "Metadata Test", "Local metadata only", isDefault: true);
            new AuditDismissalService(new FileDismissedAuditIssueStore()).Dismiss(vaultPath, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))));
            new ActivityLogService(new SqliteActivityLogStore()).Append(
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

            var secretValues = new[]
            {
                secret,
                cardNumber,
                cvc,
                apiSecret,
                otpSeed,
                noteContent
            }.Select(Encoding.UTF8.GetBytes).ToArray();

            foreach (var file in Directory.EnumerateFiles(DefaultPaths.AppRoot, "*", SearchOption.AllDirectories))
            {
                var bytes = await File.ReadAllBytesAsync(file);
                foreach (var secretBytes in secretValues)
                    Assert.False(Contains(bytes, secretBytes), $"{file} contains a known secret.");
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
            var store = new ActivityLogService(new SqliteActivityLogStore());

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
