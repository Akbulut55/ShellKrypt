using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Konscious.Security.Cryptography;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed class SqliteVaultService : IVaultService
{
    private const int Version = 1;

    private const int KeySize = 32;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private static VaultKdfParams DefaultKdf()
        => NormalizeKdf(VaultSecurityProfiles.Default.Kdf);

    public async Task CreateAsync(string vaultPath, string masterPassword, VaultKdfParams? kdf = null, CancellationToken ct = default)
    {
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

        var effectiveKdf = NormalizeKdf(kdf ?? DefaultKdf());
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var derivedKey = await DeriveKeyAsync(masterPassword, salt, effectiveKdf, ct);
        try
        {
            var vaultKey = RandomNumberGenerator.GetBytes(KeySize);
            var encryptedVaultKey = EncryptAesGcm(derivedKey, vaultKey);

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
        if (!File.Exists(vaultPath))
            return UnlockResult.Fail("Vault file not found.");

        if (string.IsNullOrWhiteSpace(masterPassword))
            return UnlockResult.Fail("Enter master password.");

        await using var conn = CreateConnection(vaultPath, SqliteOpenMode.ReadWrite);
        await conn.OpenAsync(ct);
        await ConfigureConnectionAsync(conn, ct);

        var meta = await ReadVaultMetaAsync(conn, ct);
        if (meta is null)
            return UnlockResult.Fail("Vault metadata missing or corrupted.");

        var derivedKey = await DeriveKeyAsync(masterPassword, meta.Value.Salt, meta.Value.Kdf, ct);
        try
        {
            try
            {
                var vaultKey = DecryptAesGcm(derivedKey, meta.Value.EncryptedVaultKey);
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

        var meta = await ReadVaultMetaAsync(conn, ct);
        if (meta is null)
            return ChangeMasterPasswordResult.Fail("Vault metadata missing or corrupted.");

        var currentDerivedKey = await DeriveKeyAsync(currentMasterPassword, meta.Value.Salt, meta.Value.Kdf, ct);
        try
        {
            byte[] vaultKey;
            try
            {
                vaultKey = DecryptAesGcm(currentDerivedKey, meta.Value.EncryptedVaultKey);
            }
            catch (CryptographicException)
            {
                return ChangeMasterPasswordResult.Fail("Wrong current master password.");
            }

            try
            {
                var effectiveKdf = NormalizeKdf(newKdf ?? DefaultKdf());
                var newSalt = RandomNumberGenerator.GetBytes(SaltSize);
                var newDerivedKey = await DeriveKeyAsync(newMasterPassword, newSalt, effectiveKdf, ct);
                try
                {
                    var rewrappedVaultKey = EncryptAesGcm(newDerivedKey, vaultKey);
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
        if (!File.Exists(vaultPath))
            return null;

        await using var conn = CreateConnection(vaultPath, SqliteOpenMode.ReadWrite);
        await conn.OpenAsync(ct);
        await ConfigureConnectionAsync(conn, ct);

        var meta = await ReadVaultMetaAsync(conn, ct);
        return meta?.Kdf;
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

        CREATE UNIQUE INDEX IF NOT EXISTS idx_labels_name ON labels(name COLLATE NOCASE);
        CREATE INDEX IF NOT EXISTS idx_item_labels_itemId ON item_labels(itemId);
        CREATE INDEX IF NOT EXISTS idx_item_labels_labelId ON item_labels(labelId);
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
        SELECT kdfMemoryKb, kdfIterations, kdfParallelism, salt, encryptedVaultKey
        FROM vault_meta WHERE id = 1 LIMIT 1;
        """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var mem = reader.GetInt32(0);
        var iters = reader.GetInt32(1);
        var par = reader.GetInt32(2);
        var salt = (byte[])reader["salt"];
        var evk = (byte[])reader["encryptedVaultKey"];

        return (new VaultKdfParams(mem, iters, par), salt, evk);
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

    private static VaultKdfParams NormalizeKdf(VaultKdfParams p)
    {
        var parallelism = Math.Clamp(p.Parallelism, 1, Math.Max(1, Environment.ProcessorCount));
        return new VaultKdfParams(
            MemoryKb: Math.Max(32768, p.MemoryKb),
            Iterations: Math.Max(3, p.Iterations),
            Parallelism: parallelism);
    }

    private static byte[] EncryptAesGcm(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        return Pack(nonce, tag, ciphertext);
    }

    private static byte[] DecryptAesGcm(byte[] key, byte[] packed)
    {
        Unpack(packed, out var nonce, out var tag, out var ciphertext);

        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static byte[] Pack(byte[] nonce, byte[] tag, byte[] ciphertext)
    {
        var blob = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, blob, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, blob, nonce.Length + tag.Length, ciphertext.Length);
        return blob;
    }

    private static void Unpack(byte[] blob, out byte[] nonce, out byte[] tag, out byte[] ciphertext)
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid ciphertext blob.");

        nonce = new byte[NonceSize];
        tag = new byte[TagSize];
        ciphertext = new byte[blob.Length - NonceSize - TagSize];

        Buffer.BlockCopy(blob, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(blob, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(blob, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);
    }
}
