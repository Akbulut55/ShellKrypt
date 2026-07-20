using Microsoft.Data.Sqlite;
using ShellKrypt.Application.Activity;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Crypto;
using ShellKrypt.Infrastructure.Items;
using ShellKrypt.Infrastructure.Services;
using ShellKrypt.Infrastructure.Vaulting;
using Xunit;

namespace ShellKrypt.Tests;

public sealed class VaultSecurityHardeningTests
{
    [Fact]
    public async Task ChangeMasterPasswordAsync_RewrapsVaultKeyAndRejectsOldPassword()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var vaultPath = workspace.FilePath("vault.skvault");

        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");

        var result = await vaultService.ChangeMasterPasswordAsync(
            vaultPath,
            "Vault Master Passphrase 2026",
            "Updated Vault Passphrase 2026",
            VaultSecurityProfiles.FromKey("maximum").Kdf);

        Assert.True(result.Success, result.Error);

        var oldUnlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
        Assert.False(oldUnlock.Success);

        var newUnlock = await vaultService.UnlockAsync(vaultPath, "Updated Vault Passphrase 2026");
        Assert.True(newUnlock.Success, newUnlock.Error);
        Assert.NotNull(newUnlock.VaultKey);
    }

    [Fact]
    public async Task CreateAsync_StoresV2MetadataAndEnvelopeWrappedVaultKey()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var vaultPath = workspace.FilePath("vault.skvault");

        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT version, encryptedVaultKey FROM vault_meta WHERE id = 1;";
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(AesGcmBlob.HasEnvelope(reader.GetFieldValue<byte[]>(1)));
    }

    [Fact]
    public async Task UpsertLabelAsync_StoresEncryptedNameAndNonPlaintextLookup()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var vaultPath = workspace.FilePath("vault.skvault");

        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");
        var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");
        Assert.True(unlock.Success, unlock.Error);
        Assert.NotNull(unlock.VaultKey);

        await repo.UpsertLabelAsync(vaultPath, unlock.VaultKey!, "Private Label", "#123456");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        };

        await using var conn = new SqliteConnection(builder.ToString());
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name, encryptedName FROM labels LIMIT 1;";
        await using var reader = await cmd.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.NotEqual("Private Label", reader.GetString(0));
        Assert.False(reader.IsDBNull(1));
        Assert.True(reader.GetFieldValue<byte[]>(1).Length > 0);
    }

    [Fact]
    public void VaultMasterPasswordPolicy_RejectsWeakSecrets()
    {
        var weak = VaultMasterPasswordPolicy.Validate("password");
        var minimumMixed = VaultMasterPasswordPolicy.Validate("Passw0rd");
        var strongPassphrase = VaultMasterPasswordPolicy.Validate("correct horse battery");
        var strongSecret = VaultMasterPasswordPolicy.Validate("LocalVaultSecret2026");

        Assert.False(weak.IsValid);
        Assert.True(minimumMixed.IsValid);
        Assert.True(strongPassphrase.IsValid);
        Assert.True(strongSecret.IsValid);
    }

    [Fact]
    public async Task UnlockAsync_ReturnsCorruptedMetadataError_ForInvalidKdf()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var vaultPath = workspace.FilePath("vault.skvault");

        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE vault_meta SET kdfMemoryKb = 1 WHERE id = 1;";
        await cmd.ExecuteNonQueryAsync();

        var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");

        Assert.False(unlock.Success);
        Assert.Contains("KDF", unlock.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnlockAsync_ReturnsCorruptedMetadataError_ForVaultKeyWithoutEnvelope()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var vaultPath = workspace.FilePath("vault.skvault");

        await vaultService.CreateAsync(vaultPath, "Vault Master Passphrase 2026");

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE vault_meta SET encryptedVaultKey = $evk WHERE id = 1;";
        cmd.Parameters.Add("$evk", SqliteType.Blob).Value = System.Security.Cryptography.RandomNumberGenerator.GetBytes(48);
        await cmd.ExecuteNonQueryAsync();

        var unlock = await vaultService.UnlockAsync(vaultPath, "Vault Master Passphrase 2026");

        Assert.False(unlock.Success);
        Assert.Contains("metadata", unlock.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ItemPayloadEnvelope_BindsEncryptedPayloadToItemIdentity()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new WebLoginService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var first = await service.AddAsync(vaultPath, vaultKey, new WebLoginInput("First", "", "one", "", "secret-one", ""));
        var second = await service.AddAsync(vaultPath, vaultKey, new WebLoginInput("Second", "", "two", "", "secret-two", ""));

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await conn.OpenAsync();

        var swap = conn.CreateCommand();
        swap.CommandText = """
        UPDATE items
        SET encryptedPayload = (SELECT encryptedPayload FROM items WHERE id = $second)
        WHERE id = $first;
        """;
        swap.Parameters.AddWithValue("$first", first.Id);
        swap.Parameters.AddWithValue("$second", second.Id);
        await swap.ExecuteNonQueryAsync();

        await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(() => service.ListAsync(vaultPath, vaultKey));
    }

    [Theory]
    [InlineData("favorite = 1")]
    [InlineData("createdAtUtc = '2020-01-01T00:00:00.0000000+00:00'")]
    [InlineData("updatedAtUtc = '2020-01-01T00:00:00.0000000+00:00'")]
    public async Task ItemPayloadEnvelope_BindsEncryptedPayloadToHeaderMetadata(string setClause)
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new WebLoginService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var entry = await service.AddAsync(vaultPath, vaultKey, new WebLoginInput("Header", "", "user", "", "secret", ""));

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await conn.OpenAsync();

        var tamper = conn.CreateCommand();
        tamper.CommandText = $"UPDATE items SET {setClause} WHERE id = $id;";
        tamper.Parameters.AddWithValue("$id", entry.Id);
        await tamper.ExecuteNonQueryAsync();

        await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(() => service.ListAsync(vaultPath, vaultKey));
    }

    [Fact]
    public async Task ItemPayloadEnvelope_BindsEncryptedPayloadToItemType()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var repo = new SqliteItemRepository();
        var service = new WebLoginService(repo);
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");

        var entry = await service.AddAsync(vaultPath, vaultKey, new WebLoginInput("Header", "", "user", "", "secret", ""));

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await conn.OpenAsync();

        var tamper = conn.CreateCommand();
        tamper.CommandText = "UPDATE items SET type = $type WHERE id = $id;";
        tamper.Parameters.AddWithValue("$type", (int)ItemType.Card);
        tamper.Parameters.AddWithValue("$id", entry.Id);
        await tamper.ExecuteNonQueryAsync();

        var row = Assert.Single(await repo.ListAsync(vaultPath, vaultKey));
        Assert.Equal(ItemType.Card, row.Header.Type);
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
            new VaultItemPayloadReader().ReadCard(row, vaultKey));
    }

    [Fact]
    public async Task ActivityLogEnvelope_BindsEncryptedPayloadToTimestamp()
    {
        using var workspace = new TempWorkspace();
        var vaultService = new SqliteVaultService();
        var vaultPath = workspace.FilePath("vault.skvault");
        var vaultKey = await CreateAndUnlockVaultAsync(vaultService, vaultPath, "Vault Master Passphrase 2026");
        var store = new ActivityLogService(new SqliteActivityLogStore());
        var entryId = Guid.NewGuid().ToString("N");

        store.Append(
            new ActivityLogEntry(
                entryId,
                DateTimeOffset.UtcNow.ToString("O"),
                "test",
                "Tamper",
                "Safe detail.",
                "info",
                vaultPath),
            vaultKey);

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await conn.OpenAsync();
        var tamper = conn.CreateCommand();
        tamper.CommandText = "UPDATE activity_logs SET timestampUtc = $timestamp WHERE id = $id;";
        tamper.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        tamper.Parameters.AddWithValue("$id", entryId);
        await tamper.ExecuteNonQueryAsync();

        Assert.Empty(store.Load(vaultPath, vaultKey).Entries);
    }

    [Fact]
    public void VaultFileGuard_DeleteVaultAndKnownSidecars_DeletesOnlyExactVaultSidecars()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("delete-me.skvault");
        File.WriteAllText(vaultPath, "vault");
        File.WriteAllText(vaultPath + "-wal", "wal");
        File.WriteAllText(vaultPath + "-shm", "shm");
        File.WriteAllText(vaultPath + "-journal", "journal");
        File.WriteAllText(vaultPath + "-wal-extra", "keep");

        VaultFileGuard.DeleteVaultAndKnownSidecars(vaultPath, vaultPath);

        Assert.False(File.Exists(vaultPath));
        Assert.False(File.Exists(vaultPath + "-wal"));
        Assert.False(File.Exists(vaultPath + "-shm"));
        Assert.False(File.Exists(vaultPath + "-journal"));
        Assert.True(File.Exists(vaultPath + "-wal-extra"));
    }

    [Fact]
    public void VaultFileGuard_RefusesUnsafeDeletionTargets()
    {
        using var workspace = new TempWorkspace();
        var vaultPath = workspace.FilePath("selected.skvault");
        var otherVaultPath = workspace.FilePath("other.skvault");
        var textPath = workspace.FilePath("not-a-vault.txt");
        File.WriteAllText(vaultPath, "vault");
        File.WriteAllText(otherVaultPath, "vault");
        File.WriteAllText(textPath, "not vault");

        Assert.Throws<InvalidOperationException>(() => VaultFileGuard.DeleteVaultAndKnownSidecars(textPath, textPath));
        Assert.Throws<FileNotFoundException>(() => VaultFileGuard.DeleteVaultAndKnownSidecars(workspace.FilePath("missing.skvault"), workspace.FilePath("missing.skvault")));
        Assert.Throws<InvalidOperationException>(() => VaultFileGuard.DeleteVaultAndKnownSidecars(vaultPath, otherVaultPath));
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
