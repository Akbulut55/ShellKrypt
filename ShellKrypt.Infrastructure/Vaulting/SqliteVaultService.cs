using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed class SqliteVaultService : IVaultService
{
    private const int Version = 1;

    private const int KeySize = 32;
    private const int SaltSize = 16;

    private static VaultKdfParams DefaultKdf()
        => VaultKdfPolicy.Normalize(VaultSecurityProfiles.Default.Kdf);

    public async Task CreateAsync(string vaultPath, string masterPassword, VaultKdfParams? kdf = null, CancellationToken ct = default)
    {
        vaultPath = VaultFileGuard.EnsureVaultFilePath(vaultPath, nameof(vaultPath));

        var validation = VaultMasterPasswordPolicy.Validate(masterPassword);
        if (!validation.IsValid)
            throw new ArgumentException(validation.Message, nameof(masterPassword));

        if (File.Exists(vaultPath))
            throw new InvalidOperationException("A vault already exists at this path.");

        Directory.CreateDirectory(Path.GetDirectoryName(vaultPath)!);

        await using var conn = CreateConnection(vaultPath, SqliteOpenMode.ReadWriteCreate);
        await conn.OpenAsync(ct);

        await ConfigureConnectionAsync(conn, ct);
        await CreateSchemaAsync(conn, ct);

        var effectiveKdf = VaultKdfPolicy.Normalize(kdf ?? DefaultKdf());
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var derivedKey = await DeriveKeyAsync(masterPassword, salt, effectiveKdf, ct);
        try
        {
            var vaultKey = RandomNumberGenerator.GetBytes(KeySize);
            var encryptedVaultKey = VaultPayloadProtector.EncryptVaultKey(derivedKey, vaultKey);

            await InsertVaultMetaAsync(conn, effectiveKdf, salt, encryptedVaultKey, ct);

            CryptographicOperations.ZeroMemory(vaultKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public async Task<UnlockResult> UnlockAsync(string vaultPath, string masterPassword, CancellationToken ct = default)
    {
        try
        {
            vaultPath = VaultFileGuard.EnsureVaultFilePath(vaultPath, nameof(vaultPath));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UnlockResult.Fail(ex.Message);
        }

        if (!File.Exists(vaultPath))
            return UnlockResult.Fail("Vault file not found.");

        if (string.IsNullOrWhiteSpace(masterPassword))
            return UnlockResult.Fail("Enter master password.");

        await using var conn = CreateConnection(vaultPath, SqliteOpenMode.ReadWrite);
        await conn.OpenAsync(ct);
        await ConfigureConnectionAsync(conn, ct);

        (VaultKdfParams Kdf, byte[] Salt, byte[] EncryptedVaultKey) meta;
        try
        {
            var read = await ReadVaultMetaAsync(conn, ct);
            if (read is null)
                return UnlockResult.Fail("Vault metadata missing or corrupted.");

            meta = read.Value;
        }
        catch (InvalidDataException ex)
        {
            return UnlockResult.Fail(ex.Message);
        }
        catch (SqliteException)
        {
            return UnlockResult.Fail("Vault database is corrupted or unsupported.");
        }

        var derivedKey = await DeriveKeyAsync(masterPassword, meta.Salt, meta.Kdf, ct);
        try
        {
            try
            {
                var vaultKey = VaultPayloadProtector.DecryptVaultKey(derivedKey, meta.EncryptedVaultKey);
                return UnlockResult.Ok(vaultKey);
            }
            catch (CryptographicException)
            {
                return UnlockResult.Fail("Wrong master password.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public async Task<ChangeMasterPasswordResult> ChangeMasterPasswordAsync(
        string vaultPath,
        string currentMasterPassword,
        string newMasterPassword,
        VaultKdfParams? newKdf = null,
        CancellationToken ct = default)
    {
        try
        {
            vaultPath = VaultFileGuard.EnsureVaultFilePath(vaultPath, nameof(vaultPath));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ChangeMasterPasswordResult.Fail(ex.Message);
        }

        if (!File.Exists(vaultPath))
            return ChangeMasterPasswordResult.Fail("Vault file not found.");

        if (string.IsNullOrWhiteSpace(currentMasterPassword))
            return ChangeMasterPasswordResult.Fail("Enter the current master password.");

        var validation = VaultMasterPasswordPolicy.Validate(newMasterPassword);
        if (!validation.IsValid)
            return ChangeMasterPasswordResult.Fail(validation.Message);

        await using var conn = CreateConnection(vaultPath, SqliteOpenMode.ReadWrite);
        await conn.OpenAsync(ct);
        await ConfigureConnectionAsync(conn, ct);

        (VaultKdfParams Kdf, byte[] Salt, byte[] EncryptedVaultKey) meta;
        try
        {
            var read = await ReadVaultMetaAsync(conn, ct);
            if (read is null)
                return ChangeMasterPasswordResult.Fail("Vault metadata missing or corrupted.");

            meta = read.Value;
        }
        catch (InvalidDataException ex)
        {
            return ChangeMasterPasswordResult.Fail(ex.Message);
        }
        catch (SqliteException)
        {
            return ChangeMasterPasswordResult.Fail("Vault database is corrupted or unsupported.");
        }

        var currentDerivedKey = await DeriveKeyAsync(currentMasterPassword, meta.Salt, meta.Kdf, ct);
        try
        {
            byte[] vaultKey;
            try
            {
                vaultKey = VaultPayloadProtector.DecryptVaultKey(currentDerivedKey, meta.EncryptedVaultKey);
            }
            catch (CryptographicException)
            {
                return ChangeMasterPasswordResult.Fail("Wrong current master password.");
            }

            try
            {
                var effectiveKdf = VaultKdfPolicy.Normalize(newKdf ?? DefaultKdf());
                var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
                var newDerivedKey = await DeriveKeyAsync(newMasterPassword, newSalt, effectiveKdf, ct);
                try
                {
                    var rewrappedVaultKey = VaultPayloadProtector.EncryptVaultKey(newDerivedKey, vaultKey);
                    await UpdateVaultMetaAsync(conn, effectiveKdf, newSalt, rewrappedVaultKey, ct);
                    return ChangeMasterPasswordResult.Ok();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(newDerivedKey);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(vaultKey);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(currentDerivedKey);
        }
    }

    public async Task<VaultKdfParams?> GetKdfParamsAsync(string vaultPath, CancellationToken ct = default)
    {
        try
        {
            vaultPath = VaultFileGuard.EnsureVaultFilePath(vaultPath, nameof(vaultPath));
        }
        catch
        {
            return null;
        }

        if (!File.Exists(vaultPath))
            return null;

        await using var conn = CreateConnection(vaultPath, SqliteOpenMode.ReadWrite);
        await conn.OpenAsync(ct);
        await ConfigureConnectionAsync(conn, ct);

        try
        {
            var meta = await ReadVaultMetaAsync(conn, ct);
            return meta?.Kdf;
        }
        catch
        {
            return null;
        }
    }

    private static async Task CreateSchemaAsync(SqliteConnection conn, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        CREATE TABLE IF NOT EXISTS vault_meta (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            version INTEGER NOT NULL,
            createdAtUtc TEXT NOT NULL,
            kdfMemoryKb INTEGER NOT NULL,
            kdfIterations INTEGER NOT NULL,
            kdfParallelism INTEGER NOT NULL,
            salt BLOB NOT NULL,
            encryptedVaultKey BLOB NOT NULL
        );

        -- created now so Step 3 can start immediately
        CREATE TABLE IF NOT EXISTS items (
            id TEXT PRIMARY KEY,
            type INTEGER NOT NULL,
            favorite INTEGER NOT NULL,
            createdAtUtc TEXT NOT NULL,
            updatedAtUtc TEXT NOT NULL,
            encryptedPayload BLOB NOT NULL
        );

        CREATE TABLE IF NOT EXISTS labels (
            id TEXT PRIMARY KEY,
            encryptedName BLOB,
            name TEXT NOT NULL,
            color TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS item_labels (
            itemId TEXT NOT NULL,
            labelId TEXT NOT NULL,
            PRIMARY KEY (itemId, labelId),
            FOREIGN KEY (itemId) REFERENCES items(id) ON DELETE CASCADE,
            FOREIGN KEY (labelId) REFERENCES labels(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS activity_logs (
            id TEXT PRIMARY KEY,
            timestampUtc TEXT NOT NULL,
            encryptedPayload BLOB NOT NULL
        );

        CREATE UNIQUE INDEX IF NOT EXISTS idx_labels_name ON labels(name COLLATE NOCASE);
        CREATE INDEX IF NOT EXISTS idx_item_labels_itemId ON item_labels(itemId);
        CREATE INDEX IF NOT EXISTS idx_item_labels_labelId ON item_labels(labelId);
        CREATE INDEX IF NOT EXISTS idx_activity_logs_timestampUtc ON activity_logs(timestampUtc DESC);
        """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static SqliteConnection CreateConnection(string vaultPath, SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = vaultPath,
            Mode = mode,
            Pooling = false
        };

        return new SqliteConnection(builder.ToString());
    }

    private static async Task ConfigureConnectionAsync(SqliteConnection conn, CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        PRAGMA foreign_keys = ON;
        PRAGMA journal_mode=DELETE;
        """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertVaultMetaAsync(
        SqliteConnection conn,
        VaultKdfParams kdf,
        byte[] salt,
        byte[] encryptedVaultKey,
        CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        INSERT INTO vault_meta
            (id, version, createdAtUtc, kdfMemoryKb, kdfIterations, kdfParallelism, salt, encryptedVaultKey)
        VALUES
            (1, $version, $createdAtUtc, $mem, $iters, $par, $salt, $evk);
        """;

        cmd.Parameters.AddWithValue("$version", Version);
        cmd.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$mem", kdf.MemoryKb);
        cmd.Parameters.AddWithValue("$iters", kdf.Iterations);
        cmd.Parameters.AddWithValue("$par", kdf.Parallelism);
        cmd.Parameters.Add("$salt", SqliteType.Blob).Value = salt;
        cmd.Parameters.Add("$evk", SqliteType.Blob).Value = encryptedVaultKey;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateVaultMetaAsync(
        SqliteConnection conn,
        VaultKdfParams kdf,
        byte[] salt,
        byte[] encryptedVaultKey,
        CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        UPDATE vault_meta
        SET kdfMemoryKb = $mem,
            kdfIterations = $iters,
            kdfParallelism = $par,
            salt = $salt,
            encryptedVaultKey = $evk
        WHERE id = 1;
        """;

        cmd.Parameters.AddWithValue("$mem", kdf.MemoryKb);
        cmd.Parameters.AddWithValue("$iters", kdf.Iterations);
        cmd.Parameters.AddWithValue("$par", kdf.Parallelism);
        cmd.Parameters.Add("$salt", SqliteType.Blob).Value = salt;
        cmd.Parameters.Add("$evk", SqliteType.Blob).Value = encryptedVaultKey;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<(VaultKdfParams Kdf, byte[] Salt, byte[] EncryptedVaultKey)?> ReadVaultMetaAsync(
        SqliteConnection conn,
        CancellationToken ct)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT version, kdfMemoryKb, kdfIterations, kdfParallelism, salt, encryptedVaultKey
        FROM vault_meta WHERE id = 1 LIMIT 1;
        """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var version = reader.GetInt32(0);
        if (version != Version)
            throw new InvalidDataException("Vault format version is unsupported.");

        var mem = reader.GetInt32(1);
        var iters = reader.GetInt32(2);
        var par = reader.GetInt32(3);
        var salt = reader.GetFieldValue<byte[]>(4);
        var evk = reader.GetFieldValue<byte[]>(5);

        if (salt.Length != SaltSize)
            throw new InvalidDataException("Vault metadata salt is corrupted.");

        if (evk.Length < AesGcmBlob.NonceSize + AesGcmBlob.TagSize)
            throw new InvalidDataException("Vault key metadata is corrupted.");

        var kdf = new VaultKdfParams(mem, iters, par);
        if (!VaultKdfPolicy.IsValidStored(kdf, out var kdfError))
            throw new InvalidDataException(kdfError);

        return (kdf, salt, evk);
    }

    private static Task<byte[]> DeriveKeyAsync(string masterPassword, byte[] salt, VaultKdfParams p, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(masterPassword))
            {
                Salt = salt,
                MemorySize = p.MemoryKb,        // KB
                Iterations = p.Iterations,
                DegreeOfParallelism = p.Parallelism
            };
            return argon2.GetBytes(KeySize);
        }, ct);
    }

}
