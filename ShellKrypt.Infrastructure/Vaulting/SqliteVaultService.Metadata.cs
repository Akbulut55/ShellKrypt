using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultService
{
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
}
