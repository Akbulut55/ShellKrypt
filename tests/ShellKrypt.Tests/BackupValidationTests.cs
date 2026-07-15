using System.Text.Json;
using ShellKrypt.Core.Backups;
using ShellKrypt.Core.Items;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Backups;
using ShellKrypt.Infrastructure.Backups.Internal;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class BackupValidationTests
{
    [Fact]
    public void LegacyBroadTransferTypes_AreRemoved()
    {
        var coreAssembly = typeof(IEncryptedVaultBackupService).Assembly;
        var infrastructureAssembly = typeof(EncryptedVaultBackupService).Assembly;

        Assert.Null(coreAssembly.GetType("ShellKrypt.Core.Vaulting.IVaultTransferService"));
        Assert.Null(infrastructureAssembly.GetType("ShellKrypt.Infrastructure.Vaulting.SqliteVaultTransferService"));
    }

    [Fact]
    public async Task PackageAndSnapshotVersions_AreValidatedIndependently()
    {
        using var workspace = new TempWorkspace();
        var packagePath = workspace.FilePath("unsupported.skbx");
        var package = new VaultEncryptedPackage(
            VaultBackupPackageCodec.CurrentVersion + 1,
            DateTimeOffset.UtcNow.ToString("O"),
            VaultSecurityProfiles.Default.Kdf,
            Convert.ToBase64String(new byte[16]),
            Convert.ToBase64String(new byte[32]));
        await File.WriteAllTextAsync(packagePath, JsonSerializer.Serialize(package, CamelCaseJson));

        var packageError = await Assert.ThrowsAsync<NotSupportedException>(
            () => new EncryptedVaultBackupService().InspectAsync(packagePath, "passphrase"));
        Assert.Contains("package version", packageError.Message, StringComparison.OrdinalIgnoreCase);

        var snapshot = ValidSnapshot() with { Version = SqliteVaultSnapshotStore.CurrentVersion + 1 };
        var snapshotError = Assert.Throws<NotSupportedException>(() => VaultSnapshotValidator.Validate(snapshot));
        Assert.Contains("snapshot version", snapshotError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotValidation_RejectsDanglingItemReference()
    {
        var snapshot = ValidSnapshot() with
        {
            ItemLabels = [new VaultSnapshotItemLabel("missing-item", "label-1")]
        };

        var error = Assert.Throws<InvalidOperationException>(() => VaultSnapshotValidator.Validate(snapshot));
        Assert.Contains("unknown item", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotValidation_RejectsDanglingLabelReference()
    {
        var snapshot = ValidSnapshot() with
        {
            ItemLabels = [new VaultSnapshotItemLabel("item-1", "missing-label")]
        };

        var error = Assert.Throws<InvalidOperationException>(() => VaultSnapshotValidator.Validate(snapshot));
        Assert.Contains("unknown label", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SnapshotValidation_RejectsDuplicateItemLabelRelationship()
    {
        var relationship = new VaultSnapshotItemLabel("item-1", "label-1");
        var snapshot = ValidSnapshot() with { ItemLabels = [relationship, relationship] };

        var error = Assert.Throws<InvalidOperationException>(() => VaultSnapshotValidator.Validate(snapshot));
        Assert.Contains("duplicate item-label", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidSnapshot_IsRejectedBeforeTargetVaultMutation()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("target.skvault");
        var vaultService = new SqliteVaultService();
        await vaultService.CreateAsync(vaultPath, "Target Vault Password 2026");
        var unlock = await vaultService.UnlockAsync(vaultPath, "Target Vault Password 2026");
        var vaultKey = Assert.IsType<byte[]>(unlock.VaultKey);
        var repo = new SqliteItemRepository();
        var header = new VaultItemHeader("existing", ItemType.Note, false, DateTimeOffset.UtcNow.ToString("O"), DateTimeOffset.UtcNow.ToString("O"));
        var payload = JsonSerializer.SerializeToUtf8Bytes(new NotePayload("Existing", "Keep me"), CamelCaseJson);
        await repo.InsertAsync(vaultPath, header, VaultPayloadProtector.EncryptItemPayload(vaultKey, header, payload));

        var invalid = ValidSnapshot() with
        {
            ItemLabels = [new VaultSnapshotItemLabel("item-1", "missing-label")]
        };
        var store = new SqliteVaultSnapshotStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.RestoreAsync(vaultPath, vaultKey, invalid));

        var rows = await repo.ListAsync(vaultPath, vaultKey);
        var existing = Assert.Single(rows);
        Assert.Equal("existing", existing.Header.Id);
    }

    private static VaultSnapshot ValidSnapshot() => new(
        SqliteVaultSnapshotStore.CurrentVersion,
        DateTimeOffset.UtcNow.ToString("O"),
        [new VaultSnapshotItem(
            "item-1",
            ItemType.Note,
            false,
            DateTimeOffset.UtcNow.ToString("O"),
            DateTimeOffset.UtcNow.ToString("O"),
            "{\"title\":\"Note\",\"content\":\"Body\"}")],
        [new VaultSnapshotLabel("label-1", "Work", null)],
        [new VaultSnapshotItemLabel("item-1", "label-1")]);

    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "ShellKrypt.BackupValidation.Tests", Guid.NewGuid().ToString("N"));
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
