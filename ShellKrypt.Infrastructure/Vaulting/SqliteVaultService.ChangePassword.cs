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
}
