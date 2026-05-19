using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Core.Items;
using ShellKrypt.Infrastructure.Items;
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
