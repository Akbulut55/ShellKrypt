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
            var encryptedVaultKey = VaultPayloadProtector.EncryptVaultKey(derivedKey, effectiveKdf, salt, vaultKey);

            await InsertVaultMetaAsync(conn, effectiveKdf, salt, encryptedVaultKey, ct);

            CryptographicOperations.ZeroMemory(vaultKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }
}
