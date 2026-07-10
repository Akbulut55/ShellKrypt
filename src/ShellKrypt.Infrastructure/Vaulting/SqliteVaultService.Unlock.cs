using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultService
{
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
                var vaultKey = VaultPayloadProtector.DecryptVaultKey(derivedKey, meta.Kdf, meta.Salt, meta.EncryptedVaultKey);
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
}
