using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using ShellKrypt.Core.Vaulting;
using ShellKrypt.Infrastructure.Crypto;

namespace ShellKrypt.Infrastructure.Vaulting;

public sealed partial class SqliteVaultTransferService
{
    private static async Task WriteTextAsync(string path, string content, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(path, content, ct);
    }

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
            var encrypted = AesGcmBlob.Encrypt(derivedKey, plaintext, BackupAssociatedData());
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

    private static void ValidateSnapshot(VaultSnapshot snapshot)
    {
        if (snapshot.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported snapshot version {snapshot.Version}.");

        if (snapshot.Items.Count > MaxSnapshotItems)
            throw new InvalidOperationException($"Snapshot contains too many items. Limit: {MaxSnapshotItems}.");

        if (snapshot.Labels.Count > MaxSnapshotLabels)
            throw new InvalidOperationException($"Snapshot contains too many labels. Limit: {MaxSnapshotLabels}.");

        if (snapshot.ItemLabels.Count > MaxSnapshotItemLabels)
            throw new InvalidOperationException($"Snapshot contains too many item-label links. Limit: {MaxSnapshotItemLabels}.");

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                throw new InvalidOperationException("Snapshot contains an item without an id.");

            if (!itemIds.Add(item.Id))
                throw new InvalidOperationException("Snapshot contains duplicate item ids.");

            if (item.PayloadJson.Length > MaxPayloadJsonChars)
                throw new InvalidOperationException("Snapshot contains an item payload that is too large.");

            _ = BuildDuplicateKey(item.Type, item.PayloadJson);
        }

        var labelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in snapshot.Labels)
        {
            if (string.IsNullOrWhiteSpace(label.Id))
                throw new InvalidOperationException("Snapshot contains a label without an id.");

            if (!labelIds.Add(label.Id))
                throw new InvalidOperationException("Snapshot contains duplicate label ids.");

            if ((label.Name?.Length ?? 0) > MaxCsvFieldChars)
                throw new InvalidOperationException("Snapshot contains a label name that is too large.");
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

        if (package.Version != PackageVersion)
            throw new NotSupportedException($"Unsupported package version {package.Version}.");

        if (!VaultKdfPolicy.IsValidStored(package.Kdf, out var kdfError))
            throw new InvalidOperationException(kdfError);

        var salt = DecodeBase64Field(package.SaltBase64, "Backup salt");
        if (salt.Length != SaltSize)
            throw new InvalidOperationException("Backup salt is invalid.");

        var encrypted = DecodeBase64Field(package.CiphertextBase64, "Backup ciphertext");
        if (encrypted.Length < AesGcmBlob.NonceSize + AesGcmBlob.TagSize)
            throw new InvalidOperationException("Backup ciphertext is invalid.");

        var derivedKey = await DeriveKeyAsync(passphrase, salt, package.Kdf, ct);
        try
        {
            var plaintext = AesGcmBlob.Decrypt(derivedKey, encrypted, BackupAssociatedData());
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

    private static void EnsureFileSize(string path, long maxBytes, string label)
    {
        var fullPath = VaultFileGuard.NormalizeFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"{label} was not found.", fullPath);

        var bytes = new FileInfo(fullPath).Length;
        if (bytes > maxBytes)
            throw new InvalidOperationException($"{label} is too large. Limit: {FormatBytes(maxBytes)}.");
    }

    private static byte[] DecodeBase64Field(string value, string label)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"{label} is not valid Base64.", ex);
        }
    }

    private static byte[] BackupAssociatedData()
        => AesGcmBlob.CreateAssociatedData("vault-backup", "v1");

    private static string? NormalizeLabelName(string name)
    {
        var normalized = name?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ComputeLabelLookupKey(string name)
    {
        var normalized = NormalizeLabelName(name) ?? string.Empty;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant()));
        return Convert.ToHexString(hash);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        decimal display = bytes;
        var unitIndex = 0;
        while (display >= 1024 && unitIndex < units.Length - 1)
        {
            display /= 1024;
            unitIndex++;
        }

        return $"{display:0.#} {units[unitIndex]}";
    }

    private static VaultKdfParams DefaultKdf()
    {
        var p = Math.Max(1, Environment.ProcessorCount / 2);
        return VaultKdfPolicy.Normalize(new VaultKdfParams(65536, 3, p));
    }

    private static Task<byte[]> DeriveKeyAsync(string passphrase, byte[] salt, VaultKdfParams p, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(passphrase))
            {
                Salt = salt,
                MemorySize = p.MemoryKb,
                Iterations = p.Iterations,
                DegreeOfParallelism = p.Parallelism
            };

            return argon2.GetBytes(KeySize);
        }, ct);
    }
}
