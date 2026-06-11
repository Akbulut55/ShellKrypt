using System.Security.Cryptography;
using System.Text.Json;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    private static async Task<VaultEncryptedPackage> CreateEncryptedPackageAsync(byte[] plaintext, string passphrase, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Export passphrase is required.", nameof(passphrase));

        if (plaintext.Length > MaxSnapshotJsonBytes)
            throw new InvalidOperationException("Vault snapshot is too large to export.");

        var kdf = DefaultKdf();
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var derivedKey = await DeriveKeyAsync(passphrase, salt, kdf, ct);
        try
        {
            var encrypted = AesGcmBlob.Encrypt(derivedKey, plaintext, BackupAssociatedData(PackageVersion, kdf, salt));
            return new VaultEncryptedPackage(
                PackageVersion,
                DateTimeOffset.UtcNow.ToString("O"),
                kdf,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(encrypted));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private static async Task<VaultSnapshot> ReadEncryptedSnapshotAsync(string packagePath, string passphrase, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Import passphrase is required.", nameof(passphrase));

        packagePath = VaultFileGuard.EnsureExtension(packagePath, VaultFileGuard.BackupExtension, "Encrypted backup file");
        EnsureFileSize(packagePath, MaxEncryptedPackageBytes, "Encrypted backup file");
        var json = await File.ReadAllTextAsync(packagePath, ct);
        var package = JsonSerializer.Deserialize<VaultEncryptedPackage>(json, JsonOptions)
            ?? throw new InvalidOperationException("Encrypted export file is empty or invalid.");

        ValidatePackageMetadata(package);

        var salt = DecodeBase64Field(package.SaltBase64, "Backup salt");
        if (salt.Length != SaltSize)
            throw new InvalidOperationException("Backup salt is invalid.");

        var encrypted = DecodeBase64Field(package.CiphertextBase64, "Backup ciphertext");
        if (!AesGcmBlob.HasEnvelope(encrypted))
            throw new InvalidOperationException("Backup ciphertext is invalid.");

        var derivedKey = await DeriveKeyAsync(passphrase, salt, package.Kdf, ct);
        try
        {
            var plaintext = AesGcmBlob.Decrypt(derivedKey, encrypted, BackupAssociatedData(package.Version, package.Kdf, salt));
            if (plaintext.Length > MaxSnapshotJsonBytes)
                throw new InvalidOperationException("Encrypted export payload is too large.");

            var snapshot = JsonSerializer.Deserialize<VaultSnapshot>(plaintext, JsonOptions)
                ?? throw new InvalidOperationException("Encrypted export payload is empty or invalid.");
            ValidateSnapshot(snapshot);
            return snapshot;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    private static byte[] BackupAssociatedData(int version, VaultKdfParams kdf, byte[] salt)
        => AesGcmBlob.CreateAssociatedData(
            "vault-backup",
            "v2",
            version.ToString(),
            kdf.MemoryKb.ToString(),
            kdf.Iterations.ToString(),
            kdf.Parallelism.ToString(),
            Convert.ToBase64String(salt));
}
